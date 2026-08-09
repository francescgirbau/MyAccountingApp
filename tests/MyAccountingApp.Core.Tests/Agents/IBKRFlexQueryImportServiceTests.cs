using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Agents;

public class IBKRFlexQueryImportServiceTests : IDisposable
{
    private readonly string _tempFile;

    public IBKRFlexQueryImportServiceTests()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public async Task ParseAllAsync_FeedsRowsToAgentsBySection()
    {
        RecordingAgent recording = new("My Section");
        IBKRFlexQueryImportService service = new(new[] { recording });

        string[] csvLines =
        {
            "My Section,Header,,Column1,Column2",
            "My Section,Data,,value1,value2",
            "My Section,Data,,value3,value4",
            "Other Section,Header,,x,y",
            string.Empty,
        };

        File.WriteAllText(this._tempFile, string.Join("\n", csvLines));

        (IEnumerable<Transaction> tx, IEnumerable<AssetTransaction> assets, IEnumerable<OptionTransaction> options) =
            await service.ParseAllAsync(this._tempFile);

        Assert.Empty(tx);
        Assert.Empty(assets);
        Assert.Equal(2, recording.ReceivedRows.Count);
        Assert.Equal("value1", recording.ReceivedRows[0][3]);
    }

    [Fact]
    public async Task ParseAllAsync_WithTradeAgent_ProducesAssetTransactions()
    {
        TradeAgent tradeAgent = new();
        IBKRFlexQueryImportService service = new(new IIBKRStatementAgent[] { tradeAgent });

        string[] csvLines =
        {
            "Trades,Header,,,,,,,,",
            "Trades,Data,Order,Stocks,USD,AAPL,\"2024-12-19, 10:00:00\",100,,,-15000.00,,,,,,",
        };

        File.WriteAllText(this._tempFile, string.Join("\n", csvLines));

        (IEnumerable<Transaction> tx, IEnumerable<AssetTransaction> assets, IEnumerable<OptionTransaction> options) =
            await service.ParseAllAsync(this._tempFile);

        AssetTransaction asset = Assert.Single(assets);
        Assert.Equal("AAPL", asset.Symbol);
        Assert.Equal(AssetTransactionType.Buy, asset.Type);
        Assert.Equal(100, asset.Quantity);
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsEmpty_WhenFileEmpty()
    {
        IBKRFlexQueryImportService service = new(new IIBKRStatementAgent[] { new RecordingAgent("X") });
        File.WriteAllText(this._tempFile, string.Empty);

        (IEnumerable<Transaction> tx, IEnumerable<AssetTransaction> assets, IEnumerable<OptionTransaction> options) =
            await service.ParseAllAsync(this._tempFile);

        Assert.Empty(tx);
        Assert.Empty(assets);
        Assert.Empty(options);
    }

    [Fact]
    public async Task ParseAllAsync_Throws_WhenPathNullOrEmpty()
    {
        IBKRFlexQueryImportService service = new(new IIBKRStatementAgent[] { new RecordingAgent("X") });

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ParseAllAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ParseAllAsync(string.Empty));
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsEmpty()
    {
        IBKRFlexQueryImportService service = new(new IIBKRStatementAgent[] { new RecordingAgent("X") });

        IEnumerable<AssetTransaction> result = await service.ParseCorporateActionsAsync("whatever.csv");

        Assert.Empty(result);
    }

    private sealed class RecordingAgent : IIBKRStatementAgent
    {
        public RecordingAgent(string sectionName)
        {
            this.SectionName = sectionName;
        }

        public string SectionName { get; }

        public List<string[]> ReceivedRows { get; } = new();

        public void Parse(IReadOnlyList<string[]> rows, List<Transaction> transactions, List<AssetTransaction> assetTransactions, List<OptionTransaction> optionTransactions, List<string> errors)
        {
            this.ReceivedRows.AddRange(rows);
        }
    }
}
