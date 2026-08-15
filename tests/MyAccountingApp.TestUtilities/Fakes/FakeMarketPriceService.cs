using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.TestUtilities.Fakes
{
    public class FakeMarketPriceService : IMarketPriceService
    {
        private readonly Dictionary<string, Money> _prices;
        private readonly HashSet<string> _staleSymbols;

        public FakeMarketPriceService(Dictionary<string, Money>? prices = null, HashSet<string>? staleSymbols = null)
        {
            _prices = prices ?? new Dictionary<string, Money>
            {
                { "AAPL", new Money(150.25m, "USD") },
                { "TSLA", new Money(720.50m, "USD") },
                { "BMW.DE", new Money(80.75m, "EUR") }
            };
            _staleSymbols = staleSymbols ?? new HashSet<string>();
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

        public Task<Money?> RefreshPriceAsync(string symbol) => this.GetPriceAsync(symbol);

        public Task<Money?> GetCachedPriceAsync(string symbol) =>
            Task.FromResult(!_staleSymbols.Contains(symbol) && _prices.TryGetValue(symbol, out Money? price) ? price : null);

        public Task<CachedQuote?> GetLastQuoteAsync(string symbol) =>
            Task.FromResult(_prices.TryGetValue(symbol, out Money? price) ? new CachedQuote(price, DateTimeOffset.UtcNow) : null);
    }
}
