using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-12-cd4ca: a bug written into a draft — title and description required, each criterion one listed line.
/// 2026-11-15-67f1: <c>source</c> is the file text written, kept as the bug's Source.</summary>
public static class BugDraftWrite
{
    public static DraftWrite Write(ProposalDraft draft, string? title, string? description, IReadOnlyList<string> criteria,
        string? source = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(criteria);
        if (DraftKind.Conflict(draft, DraftKind.Bug) is { } conflict) return DraftWrite.Refused(conflict);
        if (string.IsNullOrWhiteSpace(title)) return DraftWrite.Refused("the bug has no title");
        if (string.IsNullOrWhiteSpace(description)) return DraftWrite.Refused("the bug has no description");
        var lines = criteria.Select(c => CriterionLine.Collapse(c ?? string.Empty)).Where(l => l.Length > 0).ToList();
        var listed = lines.Count == 0 ? null : string.Join("\n", lines.Select(l => $"- {l}"));
        return DraftWrite.To(draft with { Bug = new BugTicketDraft(title.Trim(), description.Trim(), listed) { Source = source } });
    }
}
