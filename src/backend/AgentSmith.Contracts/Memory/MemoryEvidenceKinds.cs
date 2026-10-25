namespace AgentSmith.Contracts.Memory;

/// <summary>
/// 2026-10-25-9324a: what one ledger row says about a memory's use — a spec <c>relied</c> on it, the spec's
/// outcome <c>confirmed</c> or <c>strained</c> it, or a person's judgement or a master's dispute
/// <c>contradicted</c> it.
/// </summary>
public static class MemoryEvidenceKinds
{
    public const string Relied = "relied";
    public const string Confirmed = "confirmed";
    public const string Strained = "strained";
    public const string Contradicted = "contradicted";
}
