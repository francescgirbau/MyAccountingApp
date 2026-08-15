using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.Coinbase;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using Xunit;

namespace MyAccountingApp.Core.Tests.Agents;

public class CoinbaseImportServiceTests
{
    private const string FixtureCsv = """"
        User,Francesc Girbau Llistuella,919bdb40-bca7-58ba-b521-babd4b341ec4
        ID,Timestamp,Transaction Type,Asset,Quantity Transacted,Price Currency,Price at Transaction,Subtotal,Total (inclusive of fees and/or spread),Fees and/or Spread,Notes,Sender Address,Recipient Address
        61cf7881dcb36d00016f97c6,2021-12-31 21:39:13 UTC,Buy,ADA,212.524476,EUR,€1.1511997794,€244.65813,€250.00,1.6718701116994,Bought 212.524476 ADA for 250 EUR using EUR Wallet,,
        61cf775ac8e0830001ba2d5b,2021-12-31 21:34:18 UTC,Buy,BTC,0.0120548,EUR,€40562.4468156,€488.97218,€500.00,3.6878161273051,Bought 0.0120548 BTC for 500 EUR using EUR Wallet,,
        61cf74b799c6030001468c25,2021-12-31 21:23:03 UTC,Deposit,EUR,1000,EUR,€1.00,€1000.00,€1000.00,€0.00,Deposit from ABN AMRO BANK NV (NL29 ABNA 0889 7749 27),,
        61aa7bcbd455e900010c3ec8,2021-12-03 20:19:23 UTC,Buy,BTC,0.02125675,EUR,€46875.142564575,€996.41319,€1000.00,-11.0931867095297,Bought 0.02125675 BTC for 1000 EUR using EUR Wallet,,
        61aa7acddc3e590001a6258f,2021-12-03 20:15:09 UTC,Deposit,EUR,1000,EUR,€1.00,€1000.00,€1000.00,€0.00,Deposit from ABN AMRO BANK NV (NL29 ABNA 0889 7749 27),,
        61a0000000000000000001,2022-01-15 10:00:00 UTC,Withdrawal,EUR,250,EUR,€1.00,€250.00,€250.00,€0.00,Withdrawal to NL29 ABNA 0889 7749 27,,
        61a0000000000000000002,2022-01-16 11:00:00 UTC,Sell,BTC,0.005,EUR,€40000.00,€200.00,€200.00,€0.00,Sold 0.005 BTC for 200 EUR using EUR Wallet,,
        61a0000000000000000003,2022-01-17 12:00:00 UTC,Rewards Income,ADA,5,EUR,€1.00,€5.00,€5.00,€0.00,ADA rewards earned,,0x1234567890abcdef
        """";

    [Fact]
    public async Task ParseAllAsync_SkipsUserAndHeaderLines()
    {
        string path = CreateFixtureFile();
        CoinbaseImportService service = new CoinbaseImportService();

        var (transactions, assetTransactions, _) = await service.ParseAllAsync(path);

        Assert.Equal(3, transactions.Count());
        Assert.Equal(4, assetTransactions.Count());
    }

    [Fact]
    public async Task ParseAllAsync_Buys_BecomeBuyAssetTransactions()
    {
        string path = CreateFixtureFile();
        CoinbaseImportService service = new CoinbaseImportService();

        var (_, assetTransactions, _) = await service.ParseAllAsync(path);

        Assert.Equal(3, assetTransactions.Where(at => at.Type == AssetTransactionType.Buy).Count());
        Assert.All(assetTransactions.Where(at => at.Type == AssetTransactionType.Buy), at => Assert.Equal(TransactionCategory.EXPENSE, at.Transaction.Category));
        AssetTransaction ada = assetTransactions.Single(at => at.Symbol == "ADA");
        Assert.Equal(212.524476m, ada.Quantity);
        Assert.Equal(250m, ada.Transaction.Money.Amount);
        AssetTransaction btc = assetTransactions.Single(at => at.Symbol == "BTC" && at.Transaction.Money.Amount == 500m);
        Assert.Equal(0.0120548m, btc.Quantity);
        Assert.Equal("EUR", btc.Transaction.Money.Currency);
        Assert.Equal("Bought 0.0120548 BTC for 500 EUR using EUR Wallet", btc.Transaction.Description);
        Assert.Equal(new DateTime(2021, 12, 31), btc.Transaction.Date.Date);
    }

    [Fact]
    public async Task ParseAllAsync_Deposits_BecomeCashDepositTransactions()
    {
        string path = CreateFixtureFile();
        CoinbaseImportService service = new CoinbaseImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Assert.Equal(2, transactions.Where(t => t.Category == TransactionCategory.DEPOSIT).Count());
        Transaction deposit = transactions.First(t => t.Category == TransactionCategory.DEPOSIT && t.Money.Amount == 1000m);
        Assert.Equal("Deposit from ABN AMRO BANK NV (NL29 ABNA 0889 7749 27)", deposit.Description);
    }

    [Fact]
    public async Task ParseAllAsync_Withdrawal_BecomeTransfer()
    {
        string path = CreateFixtureFile();
        CoinbaseImportService service = new CoinbaseImportService();

        var (transactions, _, _) = await service.ParseAllAsync(path);

        Transaction withdrawal = transactions.Single(t => t.Category == TransactionCategory.TRANSFER);
        Assert.Equal(250m, withdrawal.Money.Amount);
        Assert.Equal("Withdrawal to NL29 ABNA 0889 7749 27", withdrawal.Description);
    }

    [Fact]
    public async Task ParseAllAsync_Sell_BecomeSellAssetTransaction()
    {
        string path = CreateFixtureFile();
        CoinbaseImportService service = new CoinbaseImportService();

        var (_, assetTransactions, _) = await service.ParseAllAsync(path);

        AssetTransaction sell = assetTransactions.Single(at => at.Type == AssetTransactionType.Sell);
        Assert.Equal("BTC", sell.Symbol);
        Assert.Equal(0.005m, sell.Quantity);
        Assert.Equal(200m, sell.Transaction.Money.Amount);
        Assert.Equal(TransactionCategory.INCOME, sell.Transaction.Category);
    }

    [Fact]
    public async Task ParseAllAsync_UnsupportedTypes_AreSkipped()
    {
        string path = CreateFixtureFile();
        CoinbaseImportService service = new CoinbaseImportService();

        var (transactions, assetTransactions, _) = await service.ParseAllAsync(path);

        Assert.DoesNotContain(transactions, t => t.Description.Contains("Rewards"));
        Assert.DoesNotContain(assetTransactions, at => at.Transaction.Description.Contains("Rewards"));
    }

    private static string CreateFixtureFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"coinbase_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, FixtureCsv);
        return path;
    }
}