using System.Text.Json;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: an approved record as <see cref="SpecApprovalJson"/> writes it (web defaults,
/// camelCase) — the ApprovedSeries row and the copy a run's initial context and checkpoint carry.
/// </summary>
public sealed class SpecApprovalJsonRewriter(SpecRenameJson json)
{
    /// <summary>The rewritten record text, or null when nothing in it moved.</summary>
    public string? Rewrite(string? record) =>
        json.Rewrite(record, typeof(SpecApprovalRecord), JsonNamingPolicy.CamelCase);
}
