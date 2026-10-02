using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the limits an upload is held to, stated once. A refusal names the one that
/// was crossed; nothing is truncated.
/// </summary>
public static class ReferenceUploadLimits
{
    public const int MaxFiles = ReferenceSetLimits.MaxFiles; // 2026-10-01-283dh: stated in Contracts

    public const long MaxFileBytes = ReferenceSetLimits.MaxFileBytes;

    public const long MaxSetBytes = ReferenceSetLimits.MaxSetBytes;

    public const int MaxSetsPerConversation = 3;

    /// <summary>2026-10-02-075da: how many left-out entries an upload answer lists; the count is always whole.</summary>
    public const int MaxLeftOutListed = 20;

    /// <summary>The path column's width; a longer path is refused, never cut.</summary>
    public const int MaxPathChars = PersistenceLimits.ReferencePath;

    /// <summary>The route's body ceiling: the set bound plus room for the multipart framing.</summary>
    public const long RouteBodyBytes = 26L * 1024 * 1024;

    /// <summary>A size as a refusal states it.</summary>
    public static string Megabytes(long bytes) => ReferenceSetLimits.Megabytes(bytes);
}
