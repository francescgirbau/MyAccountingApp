namespace MyAccountingApp.Shared;

public record Money
{
    public double Amount { get; init; }
    public Currencies Currency { get; init; }

}

