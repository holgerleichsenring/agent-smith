using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the limits an uploaded website is held to, stated once. A refusal names the
/// one that was crossed; nothing is truncated and nothing is stored in part.
/// </summary>
public static class ReferenceUploadLimits
{
    public const int MaxFiles = 500;

    public const long MaxFileBytes = 5L * 1024 * 1024;

    public const long MaxSetBytes = 25L * 1024 * 1024;

    public const int MaxSetsPerConversation = 3;

    /// <summary>The path column's width; a longer path is refused, never cut.</summary>
    public const int MaxPathChars = PersistenceLimits.ReferencePath;

    /// <summary>The route's body ceiling: the set bound plus room for the multipart framing.</summary>
    public const long RouteBodyBytes = 26L * 1024 * 1024;

    /// <summary>A size as a refusal states it.</summary>
    public static string Megabytes(long bytes) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB");
}
