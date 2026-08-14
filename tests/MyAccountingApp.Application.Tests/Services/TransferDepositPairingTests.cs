using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using Xunit;

namespace MyAccountingApp.Application.Tests.Services;

public class TransferDepositPairingTests
{
    [Theory]
    [InlineData("2019-05-20")]
    [InlineData("2019-08-05")]
    [InlineData("2019-08-13")]
    [InlineData("2019-09-25")]
    [InlineData("2019-09-27")]
    [InlineData("2019-11-13")]
    public void Pair_SameDayTransferAndDeposit_Matches(string date)
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(DateTime.Parse(date), "TARGETA *9027 Revolut top-up", 200, TransactionCategory.TRANSFER),
            Tx(DateTime.Parse(date), "Top-up by *9027", 200, TransactionCategory.DEPOSIT),
        };

        IReadOnlyList<(Guid, Guid)> pairs = TransferDepositPairing.Pair(txs);

        Assert.Single(pairs);
        Assert.Empty(TransferDepositPairing.UnmatchedTransferIds(txs));
    }

    [Fact]
    public void Pair_ThreeDepositsAndThreeTransfers_MatchAll()
    {
        List<Transaction> txs = new List<Transaction>();
        for (int day = 17; day <= 19; day++)
        {
            txs.Add(Tx(new DateTime(2019, 8, day), $"Transfer {day}", 100, TransactionCategory.TRANSFER));
            txs.Add(Tx(new DateTime(2019, 8, day), $"Deposit {day}", 100, TransactionCategory.DEPOSIT));
        }

        IReadOnlyList<(Guid, Guid)> pairs = TransferDepositPairing.Pair(txs);

        Assert.Equal(3, pairs.Count);
        Assert.Empty(TransferDepositPairing.UnmatchedTransferIds(txs));
    }

    [Fact]
    public void Pair_Aug12Fixture_LeavesSingleUnmatchedTransfer()
    {
        DateTime date = new DateTime(2019, 8, 12);
        List<Transaction> txs = new List<Transaction>
        {
            Tx(date, "T 600", 600, TransactionCategory.TRANSFER),
            Tx(date, "T 300", 300, TransactionCategory.TRANSFER),
            Tx(date, "T 200 a", 200, TransactionCategory.TRANSFER),
            Tx(date, "T 200 b", 200, TransactionCategory.TRANSFER),
            Tx(date, "D 200", 200, TransactionCategory.DEPOSIT),
            Tx(date, "D 300", 300, TransactionCategory.DEPOSIT),
            Tx(date, "D 600", 600, TransactionCategory.DEPOSIT),
        };

        IReadOnlyList<(Guid, Guid)> pairs = TransferDepositPairing.Pair(txs);
        IReadOnlyList<Guid> unmatched = TransferDepositPairing.UnmatchedTransferIds(txs);

        Assert.Equal(3, pairs.Count);
        Assert.Single(unmatched);
    }

    [Fact]
    public void Pair_TransferWithTransfer_IsNotAPair()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 12), "T a", 200, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 12), "T b", 200, TransactionCategory.TRANSFER),
        };

        Assert.Empty(TransferDepositPairing.Pair(txs));
        Assert.Equal(2, TransferDepositPairing.UnmatchedTransferIds(txs).Count);
    }

    [Fact]
    public void Pair_DepositWithDeposit_NoPairsAndNoUnmatchedTransfers()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 12), "D a", 200, TransactionCategory.DEPOSIT),
            Tx(new DateTime(2019, 8, 12), "D b", 200, TransactionCategory.DEPOSIT),
        };

        Assert.Empty(TransferDepositPairing.Pair(txs));
        Assert.Empty(TransferDepositPairing.UnmatchedTransferIds(txs));
    }

    [Fact]
    public void Pair_ThreeDayWindow_Matches()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 10), "T", 200, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 13), "D", 200, TransactionCategory.DEPOSIT),
        };

        Assert.Single(TransferDepositPairing.Pair(txs));
    }

    [Fact]
    public void Pair_FourDayWindow_DoesNotMatch()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 10), "T", 200, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 14), "D", 200, TransactionCategory.DEPOSIT),
        };

        Assert.Empty(TransferDepositPairing.Pair(txs));
        Assert.Single(TransferDepositPairing.UnmatchedTransferIds(txs));
    }

    [Fact]
    public void Pair_TransferWithExpense_IsNotAPair()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 12), "T", 200, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 12), "E", 200, TransactionCategory.EXPENSE),
        };

        Assert.Empty(TransferDepositPairing.Pair(txs));
        Assert.Single(TransferDepositPairing.UnmatchedTransferIds(txs));
    }

    [Fact]
    public void Pair_OneDepositCannotPairWithTwoTransfers()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 12), "T a", 100, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 12), "T b", 100, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 12), "D", 100, TransactionCategory.DEPOSIT),
        };

        IReadOnlyList<(Guid, Guid)> pairs = TransferDepositPairing.Pair(txs);

        Assert.Single(pairs);
        Assert.Single(TransferDepositPairing.UnmatchedTransferIds(txs));
    }

    [Fact]
    public void Pair_IsDeterministic()
    {
        List<Transaction> txs = new List<Transaction>
        {
            Tx(new DateTime(2019, 8, 12), "T", 200, TransactionCategory.TRANSFER),
            Tx(new DateTime(2019, 8, 12), "D", 200, TransactionCategory.DEPOSIT),
        };

        IReadOnlyList<(Guid, Guid)> first = TransferDepositPairing.Pair(txs);
        IReadOnlyList<(Guid, Guid)> second = TransferDepositPairing.Pair(txs);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsValidPair_RejectsWrongCategoryCombinations()
    {
        DateTime date = new DateTime(2019, 8, 12);
        Transaction a = Tx(date, "a", 200, TransactionCategory.TRANSFER);
        Transaction b = Tx(date, "b", 200, TransactionCategory.DEPOSIT);

        Assert.False(TransferDepositPairing.IsValidPair(a, a));
        Assert.False(TransferDepositPairing.IsValidPair(b, b));
        Assert.False(TransferDepositPairing.IsValidPair(b, a));
        Assert.True(TransferDepositPairing.IsValidPair(a, b));
    }

    [Fact]
    public void IsValidPair_RejectsAmountOrCurrencyMismatch()
    {
        DateTime date = new DateTime(2019, 8, 12);
        Transaction transfer = Tx(date, "T", 200, TransactionCategory.TRANSFER);

        Assert.False(TransferDepositPairing.IsValidPair(transfer, Tx(date, "D", 201, TransactionCategory.DEPOSIT)));
        Assert.False(TransferDepositPairing.IsValidPair(transfer, Tx(date, "D", 200, TransactionCategory.DEPOSIT, "USD")));
    }

    private static Transaction Tx(DateTime date, string description, decimal amount, TransactionCategory category, string currency = "EUR")
    {
        return new Transaction(Guid.NewGuid(), date, description, new Money(amount, currency), category);
    }
}