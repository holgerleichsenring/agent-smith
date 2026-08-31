namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: what the reading of the client sources actually covered, travelling
/// with every difference computed from it.
/// <para>
/// <paramref name="FilesRead"/> is the read-set the tool surface recorded, not a claim the
/// reader made about itself. <paramref name="FilesNotDecided"/> is what it read and could
/// not resolve. A difference over an incomplete account is a LOWER estimate of what the
/// clients exercise, which is why the account is part of the result rather than a log line.
/// </para>
/// </summary>
public sealed record ClientExtractionAccount(
    IReadOnlyList<string> FilesRead,
    IReadOnlyList<UndecidedClientFile> FilesNotDecided,
    int CallSitesFound)
{
    public static ClientExtractionAccount Empty { get; } = new([], [], 0);

    /// <summary>True when every file the reading touched was decided.</summary>
    public bool IsComplete => FilesNotDecided.Count == 0;
}
