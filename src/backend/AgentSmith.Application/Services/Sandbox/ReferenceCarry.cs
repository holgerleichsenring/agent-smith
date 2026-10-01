using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>2026-10-01-283df: what carrying the cited sets came to — the sets written, or why none was.</summary>
public sealed record ReferenceCarry(IReadOnlyList<CarriedReferenceSet> Sets, string? Refusal)
{
    public static ReferenceCarry Refused(string refusal) => new([], refusal);
}
