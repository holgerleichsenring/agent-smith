using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-28-3793: what a restore wrote — the schema the archive was taken at, the row
/// count per table as verified against its manifest, and the total. A refusal is not this
/// shape: it carries the sentence naming the rule that stopped it.
/// </summary>
public sealed record ArchiveRestoreResponse(
    string SchemaHead,
    IReadOnlyList<ArchivedTable> Tables,
    long TotalRows);
