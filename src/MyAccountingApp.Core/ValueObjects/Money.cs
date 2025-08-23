using MyAccountingApp.Core.Enums;

namespace MyAccountingApp.Core.ValueObjects;

public record Money
{
    public double Amount { get; init; } 
        
    public Currencies Currency { get; init; }

}

