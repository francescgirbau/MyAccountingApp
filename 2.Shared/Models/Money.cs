namespace _2.Shared.Models
{
    public record Money
    {
        public double Amount { get; init; } 
        
        public Currencies Currency { get; init; }

    }
}
