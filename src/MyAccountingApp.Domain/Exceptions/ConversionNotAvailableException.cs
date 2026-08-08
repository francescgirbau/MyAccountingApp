using System;

namespace MyAccountingApp.Domain.Exceptions;

/// <summary>
/// Thrown when no conversion is available for the requested date and no stale fallback exists.
/// </summary>
public class ConversionNotAvailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionNotAvailableException"/> class.
    /// </summary>
    public ConversionNotAvailableException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionNotAvailableException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ConversionNotAvailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionNotAvailableException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConversionNotAvailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
