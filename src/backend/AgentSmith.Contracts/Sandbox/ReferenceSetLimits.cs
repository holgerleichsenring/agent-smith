namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283db: the bounds one website is held to — an upload, and since 2026-10-01-283dh a
/// directory tree render_reference copies out of a repository. Moved here from the upload route's
/// limits so the two sources of a rendered site cannot be held to two different bounds.
/// </summary>
public static class ReferenceSetLimits
{
    public const int MaxFiles = 500;

    /// <summary>2026-10-02-075da: as large as the set — a 10 MB single-file page is the material, not noise.</summary>
    public const long MaxFileBytes = MaxSetBytes;

    public const long MaxSetBytes = 25L * 1024 * 1024;

    /// <summary>The longest relative path a set may carry; the store's path column is this wide.</summary>
    public const int MaxPathChars = 240;

    /// <summary>A size as a refusal states it.</summary>
    public static string Megabytes(long bytes) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB");
}
