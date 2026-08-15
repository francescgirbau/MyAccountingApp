using System;

namespace MyAccountingApp.Domain.ValueObjects;

/// <summary>
/// A market price quote as cached by the price service, with the timestamp it was fetched at.
/// </summary>
public sealed record CachedQuote(Money Price, DateTimeOffset AsOfUtc);
