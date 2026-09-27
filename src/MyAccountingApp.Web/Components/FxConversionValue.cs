namespace MyAccountingApp.Web.Components;

/// <summary>Form payload raised by the new FX conversion dialog.</summary>
public sealed record FxConversionValue(
    DateTime Date,
    decimal FromAmount,
    string FromCurrency,
    decimal ToAmount,
    string ToCurrency,
    decimal? Rate);