using AgentSmith.Contracts.Constants;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-25-6b2e: a fact that runs only when a Copilot seat AND a runtime binary are handed to
/// the suite, and reports itself as SKIPPED WITH A REASON when either is missing.
/// <para>
/// The liveness test used to return early and say in its own summary that it "SKIPS loudly rather
/// than passing quietly" — but xunit 2.5.3 has no Assert.Skip, and returning from a [Fact] is a
/// green tick. The claim it exists to prove was reported as proven every time it could not run.
/// Modelled on RequiresSqlServerFactAttribute, which solved this in the unit suite.
/// </para>
/// </summary>
public sealed class RequiresCopilotSeatFactAttribute : FactAttribute
{
    public RequiresCopilotSeatFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(Seat) && !string.IsNullOrWhiteSpace(Runtime)) return;
        Skip = $"NOT RUN: set {AgentEnvKeys.CopilotGitHubToken} to a PERSON's token (Copilot "
            + $"rejects org-owned PATs) and {AgentEnvKeys.CopilotCliPath} to the Copilot runtime "
            + "binary. Until this has run once, the pending-call liveness is ASSUMED, not proven.";
    }

    internal static string? Seat =>
        Environment.GetEnvironmentVariable(AgentEnvKeys.CopilotGitHubToken);

    internal static string? Runtime =>
        Environment.GetEnvironmentVariable(AgentEnvKeys.CopilotCliPath);
}
