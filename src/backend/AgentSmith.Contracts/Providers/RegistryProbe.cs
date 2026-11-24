namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-23-933de: a registry probe's outcome and the sentence that says it; never a token or a body.</summary>
public sealed record RegistryProbe(RegistryProbeOutcome Outcome, string Sentence)
{
    public bool Reached => Outcome == RegistryProbeOutcome.Reached;
}
