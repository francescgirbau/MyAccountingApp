using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.DTOs;

public static class DtoMapper
{
    public static MoneyDto ToDto(this Money money) =>
        new(money.Amount, money.Currency);

    public static TransactionDto ToDto(this Transaction transaction) =>
        new(
            transaction.Id,
            transaction.Date,
            transaction.Description,
            transaction.Money.ToDto(),
            transaction.Category.ToString(),
            transaction.Source,
            transaction.FxPairId,
            transaction.FxLeg?.ToString(),
            transaction.FxBrokerRate,
            transaction.FxExternalKey);

    public static AssetTransactionDto ToDto(this AssetTransaction assetTransaction) =>
        new(
            assetTransaction.Transaction.ToDto(),
            assetTransaction.Symbol,
            assetTransaction.Quantity,
            assetTransaction.Type.ToString(),
            assetTransaction.UnitaryCost().ToDto(),
            assetTransaction.Source);

    public static ConversionDto ToDto(this Conversion conversion) =>
        new(
            conversion.Date,
            conversion.Source.ToString(),
            conversion.Quotes.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
            conversion.IsStale,
            conversion.RetrievedAtUtc,
            conversion.SourceProvider);

    public static OptionTransactionDto ToDto(this OptionTransaction optionTransaction) =>
        new(
            optionTransaction.Transaction.ToDto(),
            optionTransaction.Symbol,
            optionTransaction.Isin,
            optionTransaction.Quantity,
            optionTransaction.Type.ToString());

    public static ImportResultDto ToDto(this ImportResult result) =>
        new(
            result.Transactions.Select(t => t.ToDto()).ToList(),
            result.AssetTransactions.Select(at => at.ToDto()).ToList(),
            result.OptionTransactions.Select(o => o.ToDto()).ToList(),
            result.Errors,
            result.ValidationErrors,
            result.ValidationWarnings,
            result.FilesProcessed);
}
