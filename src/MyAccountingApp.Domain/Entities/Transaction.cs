using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Entities;

/// <summary>
/// Represents a financial transaction, including its date, description, amount, and category.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Gets the unique identifier for the transaction.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the date of the transaction.
    /// </summary>
    public DateTime Date { get; private set; }

    /// <summary>
    /// Gets the description of the transaction.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets the monetary value of the transaction.
    /// </summary>
    public Money Money { get; private set; }

    /// <summary>
    /// Gets the category of the transaction (expense, income, or transfer).
    /// </summary>
    public TransactionCategory Category { get; private set; }

    /// <summary>
    /// Gets the provenance of the transaction (e.g. the imported file name), or null.
    /// </summary>
    public string? Source { get; private set; }

    /// <summary>
    /// Gets the shared identifier of the two legs of an FX conversion, or null for non-FX transactions.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? FxPairId { get; private set; }

    /// <summary>
    /// Gets which side of the FX pair this leg is (cash out or cash in), or null for non-FX transactions.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FxLeg? FxLeg { get; private set; }

    /// <summary>
    /// Gets the broker rate of the FX pair (quote per base), stored as reported by the CSV, or null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? FxBrokerRate { get; private set; }

    /// <summary>
    /// Gets the external key identifying the pair in its source (Degiro order id, IBKR trade key), or null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FxExternalKey { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Transaction"/> class.
    /// </summary>
    /// <param name="date">The date of the transaction.</param>
    /// <param name="description">The description of the transaction.</param>
    /// <param name="money">The monetary value of the transaction.</param>
    /// <param name="category">The category of the transaction.</param>
    /// <param name="source">The provenance of the transaction.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the amount is zero, or if the amount does not match the expected sign for the category.
    /// </exception>
    public Transaction(DateTime date, string description, Money money, TransactionCategory category, string? source = null)
    {
        this.Id = Guid.NewGuid();
        this.Date = date;
        this.Description = description;
        this.Category = category;
        this.Source = source;

        this.Money = new Money(Math.Abs(money.Amount), money.Currency);

        this.Validate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Transaction"/> class.
    /// </summary>
    /// <param name="id">The id of the transaction.</param>
    /// <param name="date">The date of the transaction.</param>
    /// <param name="description">The description of the transaction.</param>
    /// <param name="money">The monetary value of the transaction.</param>
    /// <param name="category">The category of the transaction.</param>
    /// <param name="source">The provenance of the transaction.</param>
    [JsonConstructor]
    public Transaction(Guid id, DateTime date, string description, Money money, TransactionCategory category, string? source = null, Guid? fxPairId = null, FxLeg? fxLeg = null, decimal? fxBrokerRate = null, string? fxExternalKey = null)
    {
        this.Id = id;
        this.Date = date;
        this.Description = description;
        this.Money = money;
        this.Category = category;
        this.Source = source;
        this.FxPairId = fxPairId;
        this.FxLeg = fxLeg;
        this.FxBrokerRate = fxBrokerRate;
        this.FxExternalKey = fxExternalKey;
    }

    /// <summary>
    /// Validates the transaction data.
    /// Ensures the amount is not zero and matches the expected sign for the category.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown if the amount is zero, or if the amount does not match the expected sign for the category.
    /// </exception>
    private void Validate()
    {
        string parentType = nameof(Transaction);

        if (this.Money.Amount == 0)
        {
            string message = $"The {nameof(this.Money.Amount)} cannot be zero, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);
        }
    }

    public void UpdateCategory(TransactionCategory category)
    {
        if (category == TransactionCategory.FX_CONVERSION && this.FxPairId is null)
        {
            throw new ArgumentException(
                "FX_CONVERSION requires a pair: create it via /api/transactions/fx or import the broker FX row.",
                nameof(category));
        }

        if (category != TransactionCategory.FX_CONVERSION)
        {
            this.FxPairId = null;
            this.FxLeg = null;
            this.FxBrokerRate = null;
            this.FxExternalKey = null;
        }

        this.Category = category;
    }

    /// <summary>
    /// Links this transaction to an FX conversion pair as one of its legs.
    /// </summary>
    /// <param name="pairId">The shared pair identifier for both legs.</param>
    /// <param name="leg">Which side of the pair this leg is (Out sells cash, In buys cash).</param>
    /// <param name="brokerRate">The broker rate (quote per base) as reported by the source, or null.</param>
    /// <param name="externalKey">The external key identifying the pair in its source, or null.</param>
    /// <exception cref="InvalidOperationException">Thrown when the category is not FX_CONVERSION.</exception>
    /// <exception cref="ArgumentException">Thrown when the broker rate is not positive.</exception>
    public void SetFxPair(Guid pairId, FxLeg leg, decimal? brokerRate = null, string? externalKey = null)
    {
        if (this.Category != TransactionCategory.FX_CONVERSION)
        {
            throw new InvalidOperationException(
                $"Only {nameof(TransactionCategory.FX_CONVERSION)} transactions can carry an FX leg, got {this.Category}.");
        }

        if (brokerRate is <= 0)
        {
            throw new ArgumentException($"The broker rate must be positive, got {brokerRate}.", nameof(brokerRate));
        }

        this.FxPairId = pairId;
        this.FxLeg = leg;
        this.FxBrokerRate = brokerRate;
        this.FxExternalKey = externalKey;
    }

    public void SetSource(string? source)
    {
        this.Source = source;
    }

    public TransactionFingerprint GetFingerprint() => new TransactionFingerprint(
        this.Date.Date,
        Math.Abs(this.Money.Amount),
        this.Money.Currency,
        this.Description.Trim().ToUpperInvariant());
}

public record TransactionFingerprint(
    DateTime Date,
    decimal Amount,
    string Currency,
    string Description);
