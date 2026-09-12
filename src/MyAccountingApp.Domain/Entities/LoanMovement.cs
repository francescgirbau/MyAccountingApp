namespace MyAccountingApp.Domain.Entities;

using MyAccountingApp.Domain.Enums;

public class LoanMovement
{
    public Guid Id { get; }

    public Guid LoanId { get; }

    public Transaction Transaction { get; }

    public LoanMovementType Type { get; }

    public LoanMovement(Guid id, Guid loanId, Transaction transaction, LoanMovementType type)
    {
        Id = id;
        LoanId = loanId;
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Type = type;

        Validate();
    }

    private void Validate()
    {
        if (LoanId == Guid.Empty)
        {
            throw new ArgumentException("LoanId cannot be empty.", nameof(LoanId));
        }

        if (Transaction.Money.Amount <= 0)
        {
            throw new ArgumentException("Movement amount must be greater than zero.", nameof(Transaction));
        }
    }
}