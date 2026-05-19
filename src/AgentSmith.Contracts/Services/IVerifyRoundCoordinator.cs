using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Orchestrates the Verify phase's per-verifier dispatch loop. Resolves the
/// active VerifyDiff investigators against the run-state concepts, runs each
/// verifier in sequence with the Verify-phase tool surface, and returns the
/// aggregated observations plus the count of verifiers that actually ran.
/// VerifyRoundHandler keeps the two-round escalation policy: round 1 with
/// blocking observations re-loops via AgenticExecute+RunVerifyPhase; round 2
/// escalates by returning Fail.
/// </summary>
public interface IVerifyRoundCoordinator
{
    Task<VerifyRoundResult> RunRoundAsync(
        string planJson, string diffJson, AgentConfig agentConfig,
        PipelineContext pipeline, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a single Verify round. <see cref="VerifierCount"/> is the number
/// of VerifyDiff investigators that were active for this round (post
/// activates_when filtering); zero means the handler should skip without
/// counting the round.
/// </summary>
public sealed record VerifyRoundResult(
    int VerifierCount,
    IReadOnlyList<SkillObservation> Observations);
