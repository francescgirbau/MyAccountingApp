namespace MyAccountingApp.Core.Imports.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Core.Imports.AbnAmro;
using MyAccountingApp.Core.Imports.Cobas;
using MyAccountingApp.Core.Imports.Coinbase;
using MyAccountingApp.Core.Imports.Degiro;
using MyAccountingApp.Core.Imports.IBKR;
using MyAccountingApp.Core.Imports.MyInvestor;
using MyAccountingApp.Core.Imports.Revolut;
using MyAccountingApp.Core.Imports.SelfBank;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class BrokerImportDispatcher : IBrokerImportService
{
    private const string BankCsvHeader = "Data,Descripcio,Import,Moneda,Source";
    private const string DegiroCsvHeaderPrefix = "Fecha,Hora,Fecha valor";
    private const string DegiroTransactionCsvHeaderPrefix = "Fecha,Hora,Producto,ISIN,Bolsa";
    private const string RevolutCsvHeaderPrefix = "Type,Product,Started Date";
    private const string AbnAmroCsvHeaderPrefix = "accountNumber,mutationcode";
    private const string CobasCsvHeaderPrefix = "Operacion,Producto,Fecha";
    private const string MyInvestorAccountCsvHeaderPrefix = "Fecha de operaci";
    private const string MyInvestorFundCsvHeaderPrefix = "Fecha de la orden;ISIN;Importe estimado";
    private const string CoinbaseCsvHeaderPrefix = "User,";
    private const string CoinbaseCsvHeaderLinePrefix = "ID,Timestamp";

    private readonly InteractiveBrokersImportService ibkrService;
    private readonly BankCsvImportService bankService;
    private readonly AssetTransactionCsvImportService assetService;
    private readonly DegiroImportService degiroService;
    private readonly DegiroTransactionImportService degiroTransactionService;
    private readonly IBKRFlexQueryImportService flexQueryService;
    private readonly RevolutImportService revolutService;
    private readonly AbnAmroImportService abnAmroService;
    private readonly CobasImportService cobasService;
    private readonly MyInvestorAccountImportService myInvestorAccountService;
    private readonly MyInvestorFundImportService myInvestorFundService;
    private readonly SelfBankAccountImportService selfBankAccountService;
    private readonly SelfBankFundImportService selfBankFundService;
    private readonly CoinbaseImportService coinbaseService;

    public BrokerImportDispatcher(
        InteractiveBrokersImportService ibkrService,
        BankCsvImportService bankService,
        AssetTransactionCsvImportService assetService,
        DegiroImportService degiroService,
        DegiroTransactionImportService degiroTransactionService,
        IBKRFlexQueryImportService flexQueryService,
        RevolutImportService revolutService,
        AbnAmroImportService abnAmroService,
        CobasImportService cobasService,
        MyInvestorAccountImportService myInvestorAccountService,
        MyInvestorFundImportService myInvestorFundService,
        SelfBankAccountImportService selfBankAccountService,
        SelfBankFundImportService selfBankFundService,
        CoinbaseImportService coinbaseService)
    {
        this.ibkrService = ibkrService ?? throw new ArgumentNullException(nameof(ibkrService));
        this.bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
        this.assetService = assetService ?? throw new ArgumentNullException(nameof(assetService));
        this.degiroService = degiroService ?? throw new ArgumentNullException(nameof(degiroService));
        this.degiroTransactionService = degiroTransactionService ?? throw new ArgumentNullException(nameof(degiroTransactionService));
        this.flexQueryService = flexQueryService ?? throw new ArgumentNullException(nameof(flexQueryService));
        this.revolutService = revolutService ?? throw new ArgumentNullException(nameof(revolutService));
        this.abnAmroService = abnAmroService ?? throw new ArgumentNullException(nameof(abnAmroService));
        this.cobasService = cobasService ?? throw new ArgumentNullException(nameof(cobasService));
        this.myInvestorAccountService = myInvestorAccountService ?? throw new ArgumentNullException(nameof(myInvestorAccountService));
        this.myInvestorFundService = myInvestorFundService ?? throw new ArgumentNullException(nameof(myInvestorFundService));
        this.selfBankAccountService = selfBankAccountService ?? throw new ArgumentNullException(nameof(selfBankAccountService));
        this.selfBankFundService = selfBankFundService ?? throw new ArgumentNullException(nameof(selfBankFundService));
        this.coinbaseService = coinbaseService ?? throw new ArgumentNullException(nameof(coinbaseService));
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

    private static string? ReadFirstContentLine(string filePath)
    {
        try
        {
            using StreamReader reader = new StreamReader(filePath);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.TrimStart('\uFEFF');
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool HasCoinbaseSignature(string filePath)
    {
        try
        {
            using StreamReader reader = new StreamReader(filePath);
            for (int i = 0; i < 10; i++)
            {
                string? line = reader.ReadLine();
                if (line == null)
                {
                    return false;
                }

                string trimmed = line.TrimStart('\uFEFF');
                if (trimmed.StartsWith(CoinbaseCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith(CoinbaseCsvHeaderLinePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
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

        if (fileName.StartsWith("selfbank_found", StringComparison.OrdinalIgnoreCase))
        {
            return this.selfBankFundService;
        }

        if (fileName.StartsWith("selfbank", StringComparison.OrdinalIgnoreCase))
        {
            return this.selfBankAccountService;
        }

        if (fileName.EndsWith("_transactions.csv", StringComparison.OrdinalIgnoreCase))
        {
            return this.bankService;
        }

        if (HasCoinbaseSignature(filePath))
        {
            return this.coinbaseService;
        }

        string? header = ReadFirstContentLine(filePath);
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

            if (header.StartsWith(CobasCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.cobasService;
            }

            if (header.StartsWith(MyInvestorFundCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.myInvestorFundService;
            }

            if (header.StartsWith(MyInvestorAccountCsvHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return this.myInvestorAccountService;
            }
        }

        return this.ibkrService;
    }
}
