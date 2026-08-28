namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: the archive's layout, in one place. A zip whose FIRST entry is the
/// manifest, followed by one line-delimited JSON file per table — so a reader meets the
/// manifest before any table, one table is readable on its own, and neither end has to
/// hold a whole document in memory.
/// </summary>
public static class DataArchiveFormat
{
    /// <summary>Bumped when the layout changes in a way an older reader cannot take.</summary>
    public const string Version = "1";

    public const string ManifestEntry = "manifest.json";

    public const string TableDirectory = "tables/";

    public const string TableExtension = ".jsonl";

    public static string EntryFor(string table) => $"{TableDirectory}{table}{TableExtension}";
}
