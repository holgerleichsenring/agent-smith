using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: asks a fresh instance whether the premises a phase states still hold,
/// before the first master token is spent on it.
/// <para>
/// The same shape as the cut review and for the same reason: a fresh instance with no stake
/// in the spec, given a look of its own, and every answer checked against something the
/// framework can verify — here, an evidence id it minted for a look that ran. ONE call per
/// phase: there is no second pass and no conversation with the checker. What it finds is
/// reported; the spec is never rewritten in the run.
/// </para>
/// </summary>
public sealed class PhasePremiseChecker(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<PhasePremiseChecker> logger) : IPhasePremiseChecker
{
    private const string RoleName = "phase-premise-checker";

    /// <summary>The look allowance plus the turn that asks and the turn that answers, as
    /// <see cref="SpecCutReviewer.MaxIterations"/> does for the review.</summary>
    public const int MaxIterations = DerivationLookTerms.CutReviewAllowance + 2;

    public async Task<PremiseCheck> CheckAsync(
        PhaseDraft draft, PhasePremises premises, DerivationLook look,
        IReadOnlyList<PhaseProgress> alreadyRan, string key, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(premises);
        ArgumentNullException.ThrowIfNull(look);

        var prompt = PremiseCheckPrompt.For(draft, premises, look, alreadyRan);
        var answer = await AskAsync(prompt, key, look, agent, costTracker, cancellationToken);
        // A call that failed or came back unreadable is NOT a skip: nothing was decided not to
        // ask, and an operator reading the run must be able to tell the two apart.
        if (answer is null)
            return PremiseCheck.CouldNotBeTaken("the premise check returned nothing readable");

        var admitted = new PremiseCheckAdmission(logger).Admit(draft, premises, answer, look);
        foreach (var finding in admitted.False)
            logger.LogWarning(
                "Premise check — {Phase} rests on something that is no longer so: {Premise} — {Why}",
                draft.PhaseId, finding.Premise, finding.Why);
        return admitted;
    }

    private async Task<IReadOnlyList<PremiseFinding>?> AskAsync(
        string prompt, string key, DerivationLook look, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning, MaxIterations);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, key);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Plan.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = look.Tools }, ct);
            costTracker.Track(response);
            // The answer is the last turn that says anything: with tools, earlier turns may
            // carry prose around a look, and a trailing turn may carry none.
            return JsonAnswerArrayReader.Read<PremiseFindingWire, PremiseFinding>(
                response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text
                    ?? response.Text,
                w => w.ToFinding());
        }
        // Guarded on the RUN's token, never on the exception's type: the LLM layer's own
        // NetworkTimeout surfaces as a TaskCanceledException with this token NOT cancelled,
        // and a check that could not be taken must not fail a phase. An operator cancel does
        // leave the token cancelled — that still propagates.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The premise check call failed");
            return null;
        }
    }
}
