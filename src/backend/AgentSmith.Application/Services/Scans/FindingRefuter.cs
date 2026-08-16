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
/// </summary>
public sealed class FindingRefuter(
    IChatClientFactory chatClientFactory,
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
                new ChatOptions(), cancellationToken);
            costTracker.Track(response);
            return FindingRefutationReader.Read(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Silence is not a verdict: the caller ships every candidate unchanged.
            logger.LogWarning(ex, "The refutation call failed — every candidate finding stands");
            return null;
        }
    }
}
