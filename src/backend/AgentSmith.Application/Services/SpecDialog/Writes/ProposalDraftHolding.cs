using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-12-cd4ca/cd4ce: what a draft holds, in words — for a write's answer and a turn that ends without prose.</summary>
public static class ProposalDraftHolding
{
    public static string Of(ProposalDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Comment is not null) return "a comment";
        if (draft.Bug is not null) return "a bug";
        if (draft.Tickets.Count > 0) return $"{draft.Tickets.Count} ticket(s)";
        var children = draft.Specs.Count(s => !s.Parent);
        return draft.Specs.Any(s => s.Parent) ? $"a parent spec and {children} child spec(s)"
            : draft.Specs.Count == 1 ? "one spec" : "nothing";
    }
}
