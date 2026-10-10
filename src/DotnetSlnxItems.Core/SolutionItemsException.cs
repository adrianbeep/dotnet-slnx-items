namespace DotnetSlnxItems.Core;

/// <summary>
/// Thrown by <see cref="SolutionItemsService"/> to report an error
/// that the caller can act on. The <see cref="Kind"/> property carries
/// the machine-readable error kind; the <see cref="Exception.Message"/>
/// carries a human-readable description; the inner exception (if
/// present) preserves the underlying persistence-layer error.
/// </summary>
public sealed class SolutionItemsException : Exception
{
    /// <summary>The kind of error that caused this exception.</summary>
    public SolutionItemsErrorKind Kind { get; }

    /// <summary>
    /// Creates a new <see cref="SolutionItemsException"/> with the
    /// given kind and message.
    /// </summary>
    public SolutionItemsException(SolutionItemsErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>
    /// Creates a new <see cref="SolutionItemsException"/> with the
    /// given kind, message, and inner exception. Use this when
    /// wrapping an error from the underlying persistence library.
    /// </summary>
    public SolutionItemsException(SolutionItemsErrorKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }
}