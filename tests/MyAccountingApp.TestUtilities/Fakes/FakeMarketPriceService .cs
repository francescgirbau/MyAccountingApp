using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.TestUtilities.Fakes
{
    public class FakeMarketPriceService : IMarketPriceService
    {
        private readonly Dictionary<string, Money> _prices;

        public FakeMarketPriceService(Dictionary<string, Money>? prices = null)
        {
            _prices = prices ?? new Dictionary<string, Money>
            {
                { "AAPL", new Money(150.25m, "USD") },
                { "TSLA", new Money(720.50m, "USD") },
                { "BMW.DE", new Money(80.75m, "EUR") }
            };
        }

        public async Task<Money?> GetPriceAsync(string symbol)
        {
            await Task.Delay(1); // simula la crida async

            if (_prices.TryGetValue(symbol, out Money? price))
            {
                return price;
            }

            return null;
        }
    }
}
