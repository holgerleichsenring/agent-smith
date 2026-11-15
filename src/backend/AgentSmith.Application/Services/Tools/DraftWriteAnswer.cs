using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4ca: what a write tool answers — what the draft now holds, or the exact reason it was not stored.
/// 2026-11-15-06fc: named by the file.
/// </summary>
internal static class DraftWriteAnswer
{
    public static string Of(string? refusal, string done, ProposalStore store) =>
        refusal is not null ? $"Not stored: {refusal.TrimEnd('.')}." : $"{done} The draft holds {ProposalDraftHolding.Of(store.Current)}.";
}
