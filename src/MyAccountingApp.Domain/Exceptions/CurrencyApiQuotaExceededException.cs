using System;

namespace MyAccountingApp.Domain.Exceptions;

/// <summary>
/// Thrown when the external currency API reports that the request quota has been exceeded.
/// </summary>
public class CurrencyApiQuotaExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyApiQuotaExceededException"/> class.
    /// </summary>
    public CurrencyApiQuotaExceededException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyApiQuotaExceededException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CurrencyApiQuotaExceededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyApiQuotaExceededException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CurrencyApiQuotaExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
