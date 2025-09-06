using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using YahooFinanceApi;

namespace MyAccountingApp.Core.Services;
public class YahooMarketPriceService : IMarketPriceService
{
    public async Task<Money?> GetPriceAsync(string symbol)
    {
        try
        {
            IReadOnlyDictionary<string, Security> securities = await Yahoo.Symbols(symbol).Fields(Field.Symbol, Field.RegularMarketPrice).QueryAsync();

            if (securities.TryGetValue(symbol, out Security? security))
            {
                string currency = MapYahooMarketIntoCurrency(security.Market);
                double amount = security.RegularMarketPrice;

                return new Money(amount, currency);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching price for {symbol}: {ex.Message}");
            return null;
        }
    }

    private static string MapYahooMarketIntoCurrency(string market)
    {
        market = market?.ToLowerInvariant() ?? string.Empty;

        return market switch
        {
            "us_market" => "USD", // United States
            "es_market" => "EUR", // Spain
            "gb_market" => "GBP", // United Kingdom
            "ca_market" => "CAD", // Canada
            "au_market" => "AUD", // Australia
            "ch_market" => "CHF", // Switzerland
            "hk_market" => "HKD", // Hong Kong
            "no_market" => "NOK", // Norway
            "br_market" => "BRL", // Brazil
            "ar_market" => "ARS", // Argentina
            "cn_market" => "CNY", // China
            "jp_market" => "JPY", // Japan
            "se_market" => "SEK", // Sweden
            "mx_market" => "MXN", // Mexico
            "in_market" => "INR", // India
            "ru_market" => "RUB", // Russia
            "sg_market" => "SGD", // Singapore
            "tr_market" => "TRY", // Turkey
            _ => throw new NotSupportedException($"Unknown market '{market}' for currency mapping")
        };
    }

}
