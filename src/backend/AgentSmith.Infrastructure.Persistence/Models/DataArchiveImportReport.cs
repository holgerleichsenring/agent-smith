namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-28-2af6: what an import wrote — the manifest it was driven by and the rows it
/// put into each table, already verified against that manifest. An import that returns
/// this has proven the copy; one that could not throws instead.
/// </summary>
/// <param name="Manifest">The manifest the archive carried.</param>
/// <param name="Written">The row count the import wrote per table.</param>
public sealed record DataArchiveImportReport(
    DataArchiveManifest Manifest, IReadOnlyList<ArchivedTable> Written)
{
    public long TotalRows => Written.Sum(t => t.Rows);
}
