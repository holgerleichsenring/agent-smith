using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: a SECOND READING of a contradiction hand-back, not a rule — whether the code
/// says what the claim says is a model's to check. The shape of <see cref="SpecPremiseChecker"/>:
/// a fresh instance with no stake in the claim, a look of its own, ONE call, and a verdict admitted
/// only by an evidence id the framework minted for a look this check took and that ran. A
/// refutation without one is discarded and the hand-back parks as it would have.
/// </summary>
public sealed class ContradictionChecker(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<ContradictionChecker> logger) : IContradictionChecker
{
    private const string RoleName = "contradiction-checker";

    /// <summary>The look allowance plus the turn that asks and the turn that answers.</summary>
    public const int MaxIterations = DerivationLookTerms.CutReviewAllowance + 2;

    public async Task<ContradictionCheck> CheckAsync(
        SpecHandback handback, IReadOnlyList<EvidenceLook> cited, DerivationLook look, string key,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handback);
        ArgumentNullException.ThrowIfNull(look);
        var answer = await AskAsync(
            ContradictionCheckPrompt.For(handback, cited, look), key, look, agent, costTracker, cancellationToken);
        if (answer is not { Count: > 0 })
            return ContradictionCheck.Stands("the contradiction check returned nothing readable");
        return Admit(answer[0], look);
    }

    private ContradictionCheck Admit((string Verdict, string Why, string? Cites) answer, DerivationLook look)
    {
        if (!string.Equals(answer.Verdict, ContradictionCheckPrompt.Refuted, StringComparison.OrdinalIgnoreCase))
            return ContradictionCheck.Stands(answer.Why);
        var own = look.Evidence.Looks.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var proof = DerivationEvidence.CitationsIn(answer.Cites)
            .Select(id => own.GetValueOrDefault(id))
            .FirstOrDefault(l => l is { Ran: true });
        if (proof is not null) return new ContradictionCheck(true, answer.Why, proof.Line);
        logger.LogWarning(
            "The contradiction check refuted the hand-back citing {Cites}, and no look of its own that "
            + "ran minted that id — the refutation is discarded: {Why}", answer.Cites ?? "nothing", answer.Why);
        return ContradictionCheck.Stands(answer.Why);
    }

    private async Task<IReadOnlyList<(string Verdict, string Why, string? Cites)>?> AskAsync(
        string prompt, string key, DerivationLook look, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning, MaxIterations);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, key);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = look.Tools }, ct);
            costTracker.Track(response);
            return JsonAnswerReader.ReadArray<ContradictionCheckWire, (string, string, string?)>(
                response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text
                    ?? response.Text,
                w => w.ToAnswer());
        }
        // Guarded on the RUN's token, as the premise check is: a check that could not be taken
        // must not fail a derivation, and an operator cancel still propagates.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The contradiction check call failed");
            return null;
        }
    }
}
