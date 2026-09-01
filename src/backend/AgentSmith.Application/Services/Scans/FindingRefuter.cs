using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: one refutation call — a fresh instance, no stake in the findings, asked what it
/// can REFUTE against the code it is shown.
/// <para>
/// The same shape and the same bookkeeping as the delivery account and the cut review:
/// its own role in the cost ledger and its own scope in the run trail, because
/// unattributed spend is invisible spend.
/// </para>
/// <para>
/// 2026-09-01-85b2: ONE call, every candidate — batching would raise the round-trip count,
/// not lower it. What the widened checked set costs is answer LENGTH, so the call states
/// its own output budget and its answer is read tolerantly.
/// </para>
/// </summary>
public sealed class FindingRefuter(
    IChatClientFactory chatClientFactory,
    IFindingRefutationReader reader,
    IRunContextAccessor runContext,
    ILogger<FindingRefuter> logger) : IFindingRefuter
{
    public const string RoleName = "finding-refuter";

    public async Task<IReadOnlyList<FindingRefutation>?> RefuteAsync(
        IReadOnlyList<CandidateFinding> candidates,
        AgentConfig agent,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) return [];
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
            using var _ = costTracker.BeginCall(
                RoleName, RoleName, SkillExecutionPhase.Verify, RoleName);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Verify.ToString(), RoleName);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, FindingRefutationPrompt.For(candidates))],
                new ChatOptions
                {
                    // One call answers every candidate, so the ANSWER is what runs out of
                    // room first. Without its own budget it truncated and the step no-opped.
                    MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Reasoning),
                },
                cancellationToken);
            costTracker.Track(response);
            var refutations = reader.Read(response.Text);
            if (refutations is null)
                logger.LogWarning(
                    "The refutation answer carried no readable verdict — every candidate "
                    + "finding stands, and nothing was checked");
            return refutations;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Silence is not a verdict: the caller ships every candidate unchanged.
            logger.LogWarning(ex, "The refutation call failed — every candidate finding stands");
            return null;
        }
    }
}
