namespace _1.Core.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public Money Amount { get; set; } // Value Object
        public string Category { get; set; }
    }
}
