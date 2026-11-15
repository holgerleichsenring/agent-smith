namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-12-cd4ca: the outcome of one write against a draft — the next draft, or why it was refused.</summary>
public sealed record DraftWrite(ProposalDraft? Next, string? Refusal)
{
    public static DraftWrite To(ProposalDraft next) => new(next, null);

    public static DraftWrite Refused(string refusal) => new(null, refusal);
}
