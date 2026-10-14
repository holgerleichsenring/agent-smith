using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: the one Reasoning call of a set-scope round — a fresh instance, no memory, the look's tools,
/// under its own cost role and call scope, guarded on the run's token — and what
/// <see cref="SeriesContractAdmission"/> keeps of its answer.
/// </summary>
public sealed class SeriesContractReviewer(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    SeriesContractAdmission admission,
    ILogger<SeriesContractReviewer> logger) : ISeriesContractReviewer
{
    public const string RoleName = "series-contract-reviewer";
    private const string Key = "series";

    public async Task<SeriesContractReview> ReviewAsync(
        SeriesContractInput input, DerivationLook? look, AgentConfig agent, PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(costTracker);
        if (input.InRun && look is null) return SeriesContractReview.Failed("the run holds no repository to open");
        var answer = await AskAsync(SeriesContractPrompt.For(input, look), look, agent, costTracker, cancellationToken);
        return answer is null
            ? SeriesContractReview.Failed("the reviewer call failed or its answer could not be read")
            : admission.Admit(answer, input, look);
    }

    private async Task<SeriesContractAnswer?> AskAsync(
        string prompt, DerivationLook? look, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning, look is null ? null : look.Terms.Allowance + 2);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, Key);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString(), Key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = DerivationTools.For(look) }, ct);
            costTracker.Track(response);
            return SeriesContractAnswerReader.Read(
                response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text ?? response.Text);
        }
        // On the RUN's token, never on the exception's type: an LLM timeout arrives as a cancellation on an
        // uncancelled token, and a review must never fail its caller.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The series contract review call failed");
            return null;
        }
    }
}
