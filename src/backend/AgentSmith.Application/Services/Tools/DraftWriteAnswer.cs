using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4ca: what a write tool answers — what the draft now holds, or the exact reason it was not stored.</summary>
internal static class DraftWriteAnswer
{
    public static string Of(string? refusal, string stored, ProposalStore store) =>
        refusal is not null ? $"Not stored: {refusal}." : $"{stored} The draft holds {Holding(store.Current)}.";

    private static string Holding(ProposalDraft draft)
    {
        if (draft.Comment is not null) return "a comment";
        if (draft.Bug is not null) return "a bug";
        if (draft.Tickets.Count > 0) return $"{draft.Tickets.Count} ticket(s)";
        var children = draft.Specs.Count(s => !s.Parent);
        return draft.Specs.Any(s => s.Parent) ? $"a parent spec and {children} child spec(s)"
            : draft.Specs.Count == 1 ? "one spec" : "nothing";
    }
}
