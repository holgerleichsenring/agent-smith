using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: what an act wants committed — a desired state, or the PoC's absence (a delete) — or why it is
/// refused, in a sentence naming the act and the state.</summary>
public sealed record PocActDecision(PocDesiredState? Desired, bool Absent, string? Refusal)
{
    public static PocActDecision Refused(string why) => new(null, false, why);
}
