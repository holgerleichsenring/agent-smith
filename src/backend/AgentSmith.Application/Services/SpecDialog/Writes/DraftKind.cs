using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: a draft holds one kind of proposal. What it holds besides <paramref name="kind"/>, named for the
/// refusal of a write of that kind, or null when the write may go in.
/// </summary>
internal static class DraftKind
{
    public const string Specs = "specs";
    public const string Tickets = "tickets";
    public const string Bug = "bug";
    public const string Comment = "comment";

    public static string? Conflict(ProposalDraft draft, string kind)
    {
        var held = new List<string>();
        if (draft.Specs.Count > 0 && kind != Specs) held.Add($"{draft.Specs.Count} spec(s)");
        if (draft.Tickets.Count > 0 && kind != Tickets) held.Add($"{draft.Tickets.Count} ticket(s)");
        if (draft.Bug is not null && kind != Bug) held.Add("a bug");
        if (draft.Comment is not null && kind != Comment) held.Add("a comment");
        return held.Count == 0 ? null
            : $"the draft holds {string.Join(" and ", held)}, and a proposal is one kind — remove_item them first";
    }
}
