using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: one fresh Reasoning call that rewrites a refused spec on the derivation's own look terms.
/// The reply must be one valid spec whose identity <see cref="SpecIdentityRule"/> holds; anything else is no
/// rewrite. Guarded on the run's token, as every review call is: a rewrite never fails its caller.
/// </summary>
public sealed class SpecRederiver(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    DerivationLookFactory looks,
    ISpecDraftValidator validator,
    SpecDraftReader reader,
    ILogger<SpecRederiver> logger) : ISpecRederiver
{
    public const string RoleName = "spec-rederiver";

    public async Task<SpecDraft?> RederiveAsync(
        PipelineContext pipeline, SpecDraft draft, IReadOnlyList<SpecReviewFinding> findings, string? widerGoal,
        AgentConfig agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        await using var look = looks.OnTerms(pipeline, DerivationLookTerms.Derivation);
        var reply = await AskAsync(SpecRederivePrompt.For(draft, findings, widerGoal, look), draft.SpecId, look,
            agent, PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        if (reply is null || validator.Validate(reply) is not SpecDraftValid valid)
        {
            logger.LogWarning("The rewrite of refused spec {Spec} gave no valid spec", draft.SpecId);
            return null;
        }
        var rewritten = reader.Read(valid.Yaml);
        if (SpecIdentityRule.Holds(draft, rewritten)) return rewritten;
        logger.LogWarning("The rewrite of refused spec {Spec} changed its id, requires, repos or applies_to", draft.SpecId);
        return null;
    }

    private async Task<string?> AskAsync(
        string prompt, string key, DerivationLook? look, AgentConfig agent, PipelineCostTracker costTracker,
        CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning, (look?.Terms.Allowance ?? 0) + 2);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, key);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = DerivationTools.For(look) }, ct);
            costTracker.Track(response);
            return response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text ?? response.Text;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The rewrite call for refused spec {Spec} failed", key);
            return null;
        }
    }
}
