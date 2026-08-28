namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-28-2af6: the first entry in a data archive, and what makes an import
/// refusable. It names the schema state the archive was taken at (the head migration's
/// NAME, never its provider-local id), the provider it came from, the application version
/// that wrote it, and the row count of every table.
/// </summary>
public sealed record DataArchiveManifest
{
    /// <summary>The archive layout's own version, so a reader meets a shape it knows.</summary>
    public string FormatVersion { get; init; } = string.Empty;

    /// <summary>The head migration's name with its timestamp prefix removed.</summary>
    public string SchemaHead { get; init; } = string.Empty;

    /// <summary>The EF provider the archive was read from, by its provider name.</summary>
    public string SourceProvider { get; init; } = string.Empty;

    /// <summary>The application version that wrote the archive.</summary>
    public string AppVersion { get; init; } = string.Empty;

    public DateTimeOffset TakenAt { get; init; }

    public IReadOnlyList<ArchivedTable> Tables { get; init; } = [];
}
