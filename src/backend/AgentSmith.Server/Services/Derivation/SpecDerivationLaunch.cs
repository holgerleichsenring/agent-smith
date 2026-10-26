namespace AgentSmith.Server.Services.Derivation;

/// <summary>2026-10-26-bdd0e: the answer to a request to derive a ticket's specs: the run, or the sentence why not.</summary>
public sealed record SpecDerivationLaunch(SpecDerivationOutcome Outcome, string? RunId, string? Reason)
{
    public static SpecDerivationLaunch Started(string runId) => new(SpecDerivationOutcome.Started, runId, null);

    public static SpecDerivationLaunch Refused(string reason) => new(SpecDerivationOutcome.Refused, null, reason);

    public static SpecDerivationLaunch UnknownProject(string project) =>
        new(SpecDerivationOutcome.UnknownProject, null, $"Project '{project}' is not configured.");

    public static SpecDerivationLaunch NoCapacity(string reason) => new(SpecDerivationOutcome.NoCapacity, null, reason);
}
