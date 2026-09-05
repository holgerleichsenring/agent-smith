using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// One spec-review call — the model call itself, with the bookkeeping every model call in
/// this system owes: its own role in the cost ledger and its own scope in the run trail.
/// Unattributed spend is invisible spend, and a gate that spends before the master runs is
/// exactly the spend an operator will ask about first.
/// </summary>
public sealed class SpecReviewCall(
    IRunContextAccessor runContext,
    ILogger<SpecReviewCall> logger)
{
    public const string RoleName = "spec-review";

    public async Task<IReadOnlyList<CriterionReview>?> AskAsync(
        IChatClient chat, string phaseId, string goal, IReadOnlyList<string> criteria,
        IList<AITool>? tools, PipelineCostTracker costTracker, CancellationToken ct)
    {
        var prompt = SpecReviewPrompt.For(goal, criteria);
        try
        {
            using var _ = costTracker.BeginCall(
                RoleName, RoleName, SkillExecutionPhase.Review, phaseId);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Review.ToString(), phaseId);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = tools }, ct);
            costTracker.Track(response);
            return SpecReviewReader.Read(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The spec review call failed for phase {Phase}", phaseId);
            return null;
        }
    }
}
