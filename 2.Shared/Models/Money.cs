namespace _2.Shared.Models
{
    public record Money
    {
        public double Value { get; init; } 
        
        public Currencies Currency { get; init; }

    }
}
