namespace _2.Shared.Models
{
    public record Money
    {
        public decimal Amount { get; init; } 
        
        public Currencies Currency { get; init; }

    }
}
