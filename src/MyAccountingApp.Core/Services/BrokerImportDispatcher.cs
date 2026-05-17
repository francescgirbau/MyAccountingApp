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
    private static readonly string BankHeader = "Data,Descripcio,Import,Moneda,Source,Categoria";

    private readonly InteractiveBrokersImportService ibkrService;
    private readonly BankCsvImportService bankService;

    public BrokerImportDispatcher(
        InteractiveBrokersImportService ibkrService,
        BankCsvImportService bankService)
    {
        this.ibkrService = ibkrService ?? throw new ArgumentNullException(nameof(ibkrService));
        this.bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
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

    private IBrokerImportService SelectService(string filePath)
    {
        string? firstLine = File.ReadLines(filePath).FirstOrDefault();

        if (firstLine != null && firstLine.Trim().StartsWith(BankHeader, StringComparison.OrdinalIgnoreCase))
        {
            return this.bankService;
        }

        return this.ibkrService;
    }
}
