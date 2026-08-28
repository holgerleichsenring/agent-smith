using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-28-3793: what an archive taken right now WOULD carry — the schema it would be
/// taken at, the provider behind it, and every table with its row count.
/// <para>
/// It is read before the download because a full archive is the whole installation in
/// clear, and "download" is a poor moment to find out what that means. The byte size is
/// deliberately absent: the archive is written as it is produced, so nothing knows how
/// large it is until it has been written, and a number invented here would be a guess
/// wearing a fact's clothes.
/// </para>
/// </summary>
public sealed record ArchivePreviewResponse(
    string SchemaHead,
    string Provider,
    IReadOnlyList<ArchivedTable> Tables,
    long TotalRows);
