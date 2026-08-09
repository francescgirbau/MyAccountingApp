namespace MyAccountingApp.Core.Imports.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.AbnAmro;
using MyAccountingApp.Core.Imports.Degiro;
using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Core.Imports.Revolut;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class BrokerImportDispatcher : IBrokerImportService
{
    private const string BankCsvHeader = "Data,Descripcio,Import,Moneda,Source";
    private const string DegiroCsvHeaderPrefix = "Fecha,Hora,Fecha valor";
    private const string DegiroTransactionCsvHeaderPrefix = "Fecha,Hora,Producto,ISIN,Bolsa";
    private const string RevolutCsvHeaderPrefix = "Type,Product,Started Date";
    private const string AbnAmroCsvHeaderPrefix = "accountNumber,mutationcode";

    private readonly InteractiveBrokersImportService ibkrService;
    private readonly BankCsvImportService bankService;
    private readonly AssetTransactionCsvImportService assetService;
    private readonly DegiroImportService degiroService;
    private readonly DegiroTransactionImportService degiroTransactionService;
    private readonly IBKRFlexQueryImportService flexQueryService;
    private readonly RevolutImportService revolutService;
    private readonly AbnAmroImportService abnAmroService;

    public BrokerImportDispatcher(
        InteractiveBrokersImportService ibkrService,
        BankCsvImportService bankService,
        AssetTransactionCsvImportService assetService,
        DegiroImportService degiroService,
        DegiroTransactionImportService degiroTransactionService,
        IBKRFlexQueryImportService flexQueryService,
        RevolutImportService revolutService,
        AbnAmroImportService abnAmroService)
    {
        this.ibkrService = ibkrService ?? throw new ArgumentNullException(nameof(ibkrService));
        this.bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
        this.assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        this.degiroService = degiroService ?? throw new ArgumentNullException(nameof(degiroService));
        this.degiroTransactionService = degiroTransactionService ?? throw new ArgumentNullException(nameof(degiroTransactionService));
        this.flexQueryService = flexQueryService ?? throw new ArgumentNullException(nameof(flexQueryService));
        this.revolutService = revolutService ?? throw new ArgumentNullException(nameof(revolutService));
        this.abnAmroService = abnAmroService ?? throw new ArgumentNullException(nameof(abnAmroService));
    }

    public Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
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

        if (fileName.StartsWith("U8997440_", StringComparison.OrdinalIgnoreCase))
        {
            return this.flexQueryService;
        }

        if (fileName.EndsWith("_asset_transactions.csv", StringComparison.OrdinalIgnoreCase))
        {
            return this.assetService;
        }

        if (fileName.StartsWith("ABN_", StringComparison.OrdinalIgnoreCase))
        {
            return this.abnAmroService;
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

            if (header.StartsWith(RevolutCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.revolutService;
            }

            if (header.StartsWith(AbnAmroCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.abnAmroService;
            }

            if (header.StartsWith(DegiroTransactionCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.degiroTransactionService;
            }

            if (header.StartsWith(DegiroCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.degiroService;
            }
        }

        return this.ibkrService;
    }
}
