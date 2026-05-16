using MyAccountingApp.Core.Models;
using MyAccountingApp.Core.Services;

namespace MyAccountingApp.Core.Tests.Services;

public class InteractiveBrokersCsvParserTests
{
    private readonly InteractiveBrokersCsvParser parser = new InteractiveBrokersCsvParser();

    [Fact]
    public async Task ParseIBKRAsync_ParsesTransactionHistoryRecords()
    {
        string csv = @"Transaction History,Header,Date,Description,Transaction Type,Symbol,Quantity,Price,Price Currency,Gross Amount ,Commission,Net Amount,Transaction Fees,Multiplier,Exchange Rate
Transaction History,Data,2024-12-19,Buy 1 WHC 19DEC24 7.25 P (Assignment),Assignment,WHCTR9,1.0,-,-,-0.16555275,-0.16555275,-0.16555275,-,100.0,0.60201
Transaction History,Data,2024-12-18,VET 16JAN26 10 C,Buy,VET   260116C00010000,1.0,1.14,USD,-110.11944,-0.283943942,-110.403383942,-,100.0,0.96596";

        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, csv);

            IEnumerable<IBKRTransactionRecord> result = await this.parser.ParseIBKRAsync(filePath);
            List<IBKRTransactionRecord> records = result.ToList();

            Assert.Equal(2, records.Count);

            Assert.Equal("2024-12-19", records[0].Date);
            Assert.Equal("Buy 1 WHC 19DEC24 7.25 P (Assignment)", records[0].Description);
            Assert.Equal("Assignment", records[0].TransactionType);
            Assert.Equal("WHCTR9", records[0].Symbol);
            Assert.Equal("1.0", records[0].Quantity);
            Assert.Equal("-0.16555275", records[0].NetAmount);

            Assert.Equal("2024-12-18", records[1].Date);
            Assert.Equal("VET 16JAN26 10 C", records[1].Description);
            Assert.Equal("Buy", records[1].TransactionType);
            Assert.Equal("VET   260116C00010000", records[1].Symbol);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseIBKRAsync_SkipsStatementAndSummaryLines()
    {
        string csv = @"Statement,Header,Field Name,Field Value
Statement,Data,Title,Transaction History
Summary,Header,Field Name,Field Value
Summary,Data,Base Currency,EUR
Transaction History,Header,Date,Description,Transaction Type,Symbol,Quantity,Price,Price Currency,Gross Amount ,Commission,Net Amount,Transaction Fees,Multiplier,Exchange Rate
Transaction History,Data,2024-12-19,Test Transaction,Test,XYZ,1.0,10.0,USD,-10.0,0,-10.0,-,1.0,1.0";

        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, csv);

            IEnumerable<IBKRTransactionRecord> result = await this.parser.ParseIBKRAsync(filePath);
            List<IBKRTransactionRecord> records = result.ToList();

            Assert.Single(records);
            Assert.Equal("2024-12-19", records[0].Date);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseIBKRAsync_ReturnsEmpty_WhenNoDataLines()
    {
        string csv = @"Statement,Header,Field Name,Field Value
Statement,Data,Title,Transaction History
Transaction History,Header,Date,Description,Transaction Type,Symbol,Quantity,Price,Price Currency,Gross Amount ,Commission,Net Amount,Transaction Fees,Multiplier,Exchange Rate";

        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, csv);

            IEnumerable<IBKRTransactionRecord> result = await this.parser.ParseIBKRAsync(filePath);
            List<IBKRTransactionRecord> records = result.ToList();

            Assert.Empty(records);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseIBKRAsync_SkipsRecordsWithoutDate()
    {
        string csv = @"Transaction History,Header,Date,Description,Transaction Type,Symbol,Quantity,Price,Price Currency,Gross Amount ,Commission,Net Amount,Transaction Fees,Multiplier,Exchange Rate
Transaction History,Data,,No date record,Test,XYZ,1.0,10.0,USD,-10.0,0,-10.0,-,1.0,1.0
Transaction History,Data,2024-12-19,Valid record,Test,ABC,2.0,20.0,USD,-20.0,0,-20.0,-,1.0,1.0";

        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, csv);

            IEnumerable<IBKRTransactionRecord> result = await this.parser.ParseIBKRAsync(filePath);
            List<IBKRTransactionRecord> records = result.ToList();

            Assert.Single(records);
            Assert.Equal("2024-12-19", records[0].Date);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseIBKRAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            this.parser.ParseIBKRAsync("nonexistent_file.csv"));
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ParsesAndFiltersMergedRecords()
    {
        string csv = @"Corporate Actions,Header,Asset Category,Currency,Report Date,Date/Time,Description,Quantity,Proceeds,Value,Realized P/L,Code
Corporate Actions,Data,Stocks,CAD,2024-07-10,""2024-07-10, 19:45:00"",CVO(CA22289D1078) Tendered to CAODD89D1078 1 FOR 1,-33,0,0,0,
Corporate Actions,Data,Stocks,CAD,2024-07-17,""2024-07-15, 20:25:00"",CVO.ODD.C(CAODD89D1078) Merged(Voluntary Offer Allocation) for CAD 6.18 per Share,-33,203.94,-234.3,-28.11874,";

        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, csv);

            IEnumerable<IBKRCorporateActionRecord> result = await this.parser.ParseCorporateActionsAsync(filePath);
            List<IBKRCorporateActionRecord> records = result.ToList();

            Assert.Single(records);
            Assert.Contains("Merged", records[0].Description);
            Assert.Equal("CAD", records[0].Currency);
            Assert.Equal("2024-07-17", records[0].ReportDate);
            Assert.Equal("-33", records[0].Quantity);
            Assert.Equal("203.94", records[0].Proceeds);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsEmpty_WhenNoMergedRecords()
    {
        string csv = @"Corporate Actions,Header,Asset Category,Currency,Report Date,Date/Time,Description,Quantity,Proceeds,Value,Realized P/L,Code
Corporate Actions,Data,Stocks,CAD,2024-07-10,""2024-07-10, 19:45:00"",CVO Tendered to CAODD89D1078 1 FOR 1,-33,0,0,0,";

        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, csv);

            IEnumerable<IBKRCorporateActionRecord> result = await this.parser.ParseCorporateActionsAsync(filePath);
            List<IBKRCorporateActionRecord> records = result.ToList();

            Assert.Empty(records);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
