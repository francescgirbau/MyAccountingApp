namespace MyAccountingApp.Core.Imports.IBKR;
using System.Collections.Generic;
using MyAccountingApp.Domain.Entities;

public interface IIBKRStatementAgent
{
    string SectionName { get; }

    void Parse(IReadOnlyList<string[]> dataRows, List<Transaction> transactions, List<AssetTransaction> assetTransactions, List<OptionTransaction> optionTransactions, List<string> errors);
}
