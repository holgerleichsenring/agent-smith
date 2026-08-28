namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// 2026-08-28-2af6: thrown when a data archive cannot be written or cannot be written
/// back — a schema state the target does not share, a target that already holds rows, a
/// row the format cannot carry, or a copy whose counts disagree with the manifest.
/// </summary>
public sealed class DataArchiveException : AgentSmithException
{
    public DataArchiveException(string message)
        : base(message) { }

    public DataArchiveException(string message, Exception innerException)
        : base(message, innerException) { }
}
