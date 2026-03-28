using CsvHelper.Configuration.Attributes;

namespace MyAccountingApp.Core.Models;

public class IBKRTransactionRecord
{
    [Name("Date")]
    public string? Date { get; set; }

    [Name("Description")]
    public string? Description { get; set; }

    [Name("Transaction Type")]
    public string? TransactionType { get; set; }

    [Name("Symbol")]
    public string? Symbol { get; set; }

    [Name("Quantity")]
    public string? Quantity { get; set; }

    [Name("Price")]
    public string? Price { get; set; }

    [Name("Price Currency")]
    public string? PriceCurrency { get; set; }

    [Name("Gross Amount ")]
    public string? GrossAmount { get; set; }

    [Name("Commission")]
    public string? Commission { get; set; }

    [Name("Net Amount")]
    public string? NetAmount { get; set; }

    [Name("Transaction Fees")]
    public string? TransactionFees { get; set; }

    [Name("Multiplier")]
    public string? Multiplier { get; set; }

    [Name("Exchange Rate")]
    public string? ExchangeRate { get; set; }
}
