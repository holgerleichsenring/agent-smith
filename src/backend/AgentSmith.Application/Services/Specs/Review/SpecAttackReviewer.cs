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
/// 2026-10-13-8762e: one fresh Reasoning call that attacks one spec on the look its caller opened, and
/// keeps only what <see cref="SpecAttackAdmission"/> admits. The shape of the cut review and the diff
/// review — a fresh instance, no memory, tools of its own, guarded on the run's token — moved to the spec
/// before it is built. Iterations follow from the look's allowance plus the turn that asks and the one
/// that answers.
/// </summary>
public sealed class SpecAttackReviewer(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    SpecAttackAdmission admission,
    ILogger<SpecAttackReviewer> logger) : ISpecAttackReviewer
{
    private const string RoleName = "spec-attack-reviewer";

    public async Task<SpecAttackResult?> ReviewAsync(
        SpecAttackInput input, DerivationLook? look, AgentConfig agent, PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (look is null)
        {
            logger.LogInformation("The attack review of {Spec} was not taken: the run holds no repository to open",
                input.Draft.SpecId);
            return null;
        }
        var answer = await AskAsync(SpecAttackPrompt.For(input, look), input.Draft.SpecId, look, agent,
            costTracker, cancellationToken);
        if (answer is null)
        {
            logger.LogWarning("The attack review of {Spec} returned nothing readable", input.Draft.SpecId);
            return null;
        }
        return admission.Admit(answer, SpecRegisters.Read(input.Draft.Yaml), look, input.RepositoryNames);
    }

    private async Task<SpecAttackAnswer?> AskAsync(
        string prompt, string key, DerivationLook look, AgentConfig agent, PipelineCostTracker costTracker,
        CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning, look.Terms.Allowance + 2);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, key);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = DerivationTools.For(look) }, ct);
            costTracker.Track(response);
            // The answer is the last turn that says anything: earlier turns carry prose around a look.
            return SpecAttackAnswerReader.Read(
                response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text ?? response.Text);
        }
        // Guarded on the RUN's token, never on the exception's type: the LLM layer's own timeout surfaces as
        // a cancellation with this token not cancelled, and a review must never fail its caller.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The attack review call for {Spec} failed", key);
            return null;
        }
    }
}
