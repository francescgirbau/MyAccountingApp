namespace MyAccountingApp.Domain.Entities;

using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

public class Loan
{
    public Guid Id { get; }

    public string Counterparty { get; private set; }

    public LoanDirection Direction { get; }

    public Money Principal { get; private set; }

    public DateTime StartDate { get; }

    public string? Notes { get; private set; }

    public bool IsClosed { get; private set; }

    public Loan(
        Guid id,
        string counterparty,
        LoanDirection direction,
        Money principal,
        DateTime startDate,
        string? notes = null,
        bool isClosed = false)
    {
        Id = id;
        Counterparty = counterparty;
        Direction = direction;
        Principal = principal;
        StartDate = startDate;
        Notes = notes;
        IsClosed = isClosed;

        Validate();
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Counterparty))
        {
            throw new ArgumentException("Counterparty cannot be null or empty.", nameof(Counterparty));
        }

        if (Principal.Amount <= 0)
        {
            throw new ArgumentException("Principal must be greater than zero.", nameof(Principal));
        }
    }

    public void Rename(string counterparty)
    {
        if (string.IsNullOrWhiteSpace(counterparty))
        {
            throw new ArgumentException("Counterparty cannot be null or empty.", nameof(counterparty));
        }

        this.Counterparty = counterparty;
    }

    public void UpdateNotes(string? notes) => this.Notes = notes;

    public void Close() => this.IsClosed = true;

    public void Reopen() => this.IsClosed = false;
}