using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4ca: what a write tool answers — what the draft now holds, or the exact reason it was not stored.
/// 2026-11-15-06fc: named by the file, with the stored text when the checks changed what was written.
/// </summary>
internal static class DraftWriteAnswer
{
    public static string Of(string? refusal, string done, ProposalStore store) =>
        refusal is not null ? $"Not stored: {refusal.TrimEnd('.')}." : $"{done} The draft holds {ProposalDraftHolding.Of(store.Current)}.";

    /// <summary>The stored file's text when it differs from what was written, for the next edit to quote.</summary>
    public static string Stored(string written, string? stored) =>
        stored is null || stored.Trim() == written.Trim() ? string.Empty
            : $"\nThe checks changed what you wrote; the file reads:\n```\n{stored.TrimEnd()}\n```";
}
