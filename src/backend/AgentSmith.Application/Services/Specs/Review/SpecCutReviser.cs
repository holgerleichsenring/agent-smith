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
/// 2026-10-13-8762f: one fresh Reasoning call without tools that re-emits a spec with a round's cuts
/// applied. The reply must be one valid yaml block keeping the spec id, and <see cref="SpecRevisionGuard"/>
/// must find no change beyond the cuts; anything else returns the input draft with the refusal named. The
/// output bound follows the input — the whole spec comes back — and a reply cut at it is refused by name.
/// </summary>
public sealed class SpecCutReviser(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ISpecDraftValidator validator,
    SpecRevisionGuard guard,
    SpecDraftReader reader,
    ILogger<SpecCutReviser> logger) : ISpecCutReviser
{
    public const string RoleName = "spec-cut-reviser";

    /// <summary>The headroom over the input's own size, in tokens, before the reply could be cut.</summary>
    public const int OutputHeadroom = 2_000;

    public async Task<SpecCutRevision> ReviseAsync(
        SpecDraft draft, IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> problems,
        bool lastRound, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(cuts);
        ArgumentNullException.ThrowIfNull(problems);
        // Nothing to apply is no call: no cut, and no fact to move because this is not the last round.
        if (cuts.Count == 0 && !(lastRound && problems.Count > 0)) return new SpecCutRevision(draft, null);
        var (yaml, reason) = await ReviseYamlAsync(draft, cuts, problems, lastRound, agent, costTracker, cancellationToken);
        if (yaml is not null) return new SpecCutRevision(reader.Read(yaml), null);
        logger.LogWarning("The cut revision of {Spec} was refused: {Refusal}", draft.SpecId, reason);
        return new SpecCutRevision(draft, reason);
    }

    /// <summary>The output bound: never below the assignment's, and enough to re-emit the input whole.</summary>
    public int MaxOutputTokens(SpecDraft draft, AgentConfig agent) =>
        Math.Max(chatClientFactory.GetMaxOutputTokens(agent, TaskType.Reasoning), draft.Yaml.Length / 3 + OutputHeadroom);

    private async Task<(string? Yaml, string? Reason)> ReviseYamlAsync(
        SpecDraft draft, IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> problems,
        bool lastRound, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        var response = await AskAsync(SpecCutRevisionPrompt.For(draft, cuts, problems, lastRound),
            draft, agent, costTracker, ct);
        if (response is null) return (null, "the reviser call failed");
        if (response.FinishReason == ChatFinishReason.Length)
            return (null, "the reviser's reply was truncated at its output limit");
        var outcome = validator.Validate(response.Text);
        if (outcome is not SpecDraftValid valid) return (null, Unreadable(outcome));
        return guard.Check(draft.Yaml, valid.Yaml, cuts, problems, lastRound) is { } reason
            ? (null, reason)
            : (valid.Yaml, null);
    }

    private static string Unreadable(SpecDraftOutcome outcome) => outcome is SpecDraftInvalid invalid
        ? $"the revised spec is invalid: {invalid.Error}"
        : "the reviser's reply carried no yaml block";

    private async Task<ChatResponse?> AskAsync(
        string prompt, SpecDraft draft, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, draft.SpecId);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString(), draft.SpecId);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { MaxOutputTokens = MaxOutputTokens(draft, agent) }, ct);
            costTracker.Track(response);
            return response;
        }
        // Guarded on the RUN's token, never on the exception's type: the LLM layer's own timeout surfaces as
        // a cancellation with this token not cancelled, and a revision must never fail its caller.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The cut revision call for {Spec} failed", draft.SpecId);
            return null;
        }
    }
}
