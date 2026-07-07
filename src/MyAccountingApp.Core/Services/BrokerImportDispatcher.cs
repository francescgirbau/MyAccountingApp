namespace MyAccountingApp.Core.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class BrokerImportDispatcher : IBrokerImportService
{
    private const string BankCsvHeader = "Data,Descripcio,Import,Moneda,Source";

    private readonly InteractiveBrokersImportService ibkrService;
    private readonly BankCsvImportService bankService;
    private readonly AssetTransactionCsvImportService assetService;

    public BrokerImportDispatcher(
        InteractiveBrokersImportService ibkrService,
        BankCsvImportService bankService,
        AssetTransactionCsvImportService assetService)
    {
        this.ibkrService = ibkrService ?? throw new ArgumentNullException(nameof(ibkrService));
        this.bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
        this.assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
    }

    public Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        IBrokerImportService service = this.SelectService(filePath);
        return service.ParseAllAsync(filePath, cancellationToken);
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return this.ibkrService.ParseCorporateActionsAsync(filePath, cancellationToken);
    }

    private static string? ReadFirstLine(string filePath)
    {
        try
        {
            using StreamReader reader = new StreamReader(filePath);
            return reader.ReadLine();
        }
        catch
        {
            return null;
        }
    }

    private IBrokerImportService SelectService(string filePath)
    {
        string fileName = Path.GetFileName(filePath);

        if (fileName.EndsWith("_asset_transactions.csv", StringComparison.OrdinalIgnoreCase))
        {
            return this.assetService;
        }

        if (fileName.EndsWith("_transactions.csv", StringComparison.OrdinalIgnoreCase))
        {
            return this.bankService;
        }

        string? header = ReadFirstLine(filePath);
        if (header != null)
        {
            if (header.StartsWith(BankCsvHeader, StringComparison.OrdinalIgnoreCase))
            {
                return this.bankService;
            }

            if (header.Contains("Ticker", StringComparison.OrdinalIgnoreCase))
            {
                return this.assetService;
            }
        }

        return this.ibkrService;
    }
}
