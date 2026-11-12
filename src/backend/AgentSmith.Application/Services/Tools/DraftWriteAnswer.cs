using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4ca: what a write tool answers — what the draft now holds, or the exact reason it was not stored.</summary>
internal static class DraftWriteAnswer
{
    public static string Of(string? refusal, string stored, ProposalStore store) =>
        refusal is not null ? $"Not stored: {refusal}." : $"{stored} The draft holds {SpecDialog.Writes.ProposalDraftHolding.Of(store.Current)}.";
}
