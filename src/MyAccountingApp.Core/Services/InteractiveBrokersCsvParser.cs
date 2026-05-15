using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MyAccountingApp.Core.Models;

namespace MyAccountingApp.Core.Services;

public interface ICsvParser
{
    Task<IEnumerable<IBKRTransactionRecord>> ParseIBKRAsync(string filePath);
    Task<IEnumerable<IBKRCorporateActionRecord>> ParseCorporateActionsAsync(string filePath);
}

public class InteractiveBrokersCsvParser : ICsvParser
{
    public async Task<IEnumerable<IBKRTransactionRecord>> ParseIBKRAsync(string filePath)
    {
        CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
        };

        List<IBKRTransactionRecord> records = new List<IBKRTransactionRecord>();
        
        string[] lines = await File.ReadAllLinesAsync(filePath);
        
        string[]? headerRow = null;
        foreach (string line in lines)
        {
            if (line.StartsWith("Transaction History,Header,"))
            {
                headerRow = line.Replace("Transaction History,Header,", string.Empty).Split(',');
                continue;
            }
            
            if (!line.StartsWith("Transaction History,Data,"))
            {
                continue;
            }
            
            string dataPart = line.Replace("Transaction History,Data,", string.Empty);
            string[] values = this.ParseCsvLine(dataPart);
            
            if (headerRow == null || values.Length == 0)
            {
                continue;
            }
            
            IBKRTransactionRecord record = new IBKRTransactionRecord();
            
            for (int i = 0; i < headerRow.Length && i < values.Length; i++)
            {
                string header = headerRow[i].Trim();
                string value = values[i].Trim();
                
                switch (header)
                {
                    case "Date":
                        record.Date = value;
                        break;
                    case "Description":
                        record.Description = value;
                        break;
                    case "Transaction Type":
                        record.TransactionType = value;
                        break;
                    case "Symbol":
                        record.Symbol = value;
                        break;
                    case "Quantity":
                        record.Quantity = value;
                        break;
                    case "Price":
                        record.Price = value;
                        break;
                    case "Price Currency":
                        record.PriceCurrency = value;
                        break;
                    case "Gross Amount ":
                    case "Gross Amount":
                        record.GrossAmount = value;
                        break;
                    case "Commission":
                        record.Commission = value;
                        break;
                    case "Net Amount":
                        record.NetAmount = value;
                        break;
                    case "Transaction Fees":
                        record.TransactionFees = value;
                        break;
                    case "Multiplier":
                        record.Multiplier = value;
                        break;
                    case "Exchange Rate":
                        record.ExchangeRate = value;
                        break;
                }
            }
            
            if (!string.IsNullOrEmpty(record.Date))
            {
                records.Add(record);
            }
        }
        
        return records;
    }
    
    private string[] ParseCsvLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string current = string.Empty;
        
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = string.Empty;
            }
            else
            {
                current += c;
            }
        }
        
        result.Add(current);
        return result.ToArray();
    }

    public async Task<IEnumerable<IBKRCorporateActionRecord>> ParseCorporateActionsAsync(string filePath)
    {
        List<IBKRCorporateActionRecord> records = new List<IBKRCorporateActionRecord>();
        
        string[] lines = await File.ReadAllLinesAsync(filePath);
        
        string[]? headerRow = null;
        foreach (string line in lines)
        {
            if (line.StartsWith("Corporate Actions,Header,"))
            {
                headerRow = line.Replace("Corporate Actions,Header,", string.Empty).Split(',');
                continue;
            }
            
            if (!line.StartsWith("Corporate Actions,Data,"))
            {
                continue;
            }
            
            string dataPart = line.Replace("Corporate Actions,Data,", string.Empty);
            string[] values = this.ParseCsvLine(dataPart);
            
            if (headerRow == null || values.Length == 0)
            {
                continue;
            }
            
            IBKRCorporateActionRecord record = new IBKRCorporateActionRecord();
            
            for (int i = 0; i < headerRow.Length && i < values.Length; i++)
            {
                string header = headerRow[i].Trim();
                string value = values[i].Trim();
                
                switch (header)
                {
                    case "Asset Category":
                        record.AssetCategory = value;
                        break;
                    case "Currency":
                        record.Currency = value;
                        break;
                    case "Report Date":
                        record.ReportDate = value;
                        break;
                    case "Date/Time":
                        record.DateTime = value;
                        break;
                    case "Description":
                        record.Description = value;
                        break;
                    case "Quantity":
                        record.Quantity = value;
                        break;
                    case "Proceeds":
                        record.Proceeds = value;
                        break;
                    case "Value":
                        record.Value = value;
                        break;
                    case "Realized P/L":
                        record.RealizedPL = value;
                        break;
                    case "Code":
                        record.Code = value;
                        break;
                }
            }
            
            if (!string.IsNullOrEmpty(record.Description) && record.Description.Contains("Merged", StringComparison.OrdinalIgnoreCase))
            {
                records.Add(record);
            }
        }
        
        return records;
    }
}
