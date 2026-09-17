using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: asks a fresh instance which phases CANNOT BE DELIVERED as written, before the
/// first master token is spent on them.
/// <para>
/// The same shape as the delivery account and for the same reason: a fresh instance with
/// no stake in the cut, asked adversarially, and every finding checked against something
/// the framework can verify — here, that the quoted criterion really is in that phase.
/// Ticket 19106 lost two hours to a phase whose criteria contradicted each other; this
/// costs one call before any of them are spent.
/// </para>
/// </summary>
public sealed class SpecCutReviewer(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<SpecCutReviewer> logger) : ISpecCutReviewer
{
    private const string RoleName = "spec-cut-reviewer";

    /// <summary>
    /// 2026-09-15-ffa7: per call, the look allowance plus the turn that asks and the turn that
    /// answers — as <see cref="SpecDerivationCall.MaxIterations"/> does for the deriver. Four
    /// attempts, each with a fresh allowance, is up to twenty-four reviewer looks a derivation.
    /// </summary>
    public const int MaxIterations = DerivationLookTerms.CutReviewAllowance + 2;

    public async Task<SpecCutReview> ReviewAsync(
        SpecSet set, string ticketText, DerivationLook? look, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.Phases.Count == 0) return SpecCutReview.Clean;

        var prompt = SpecCutReviewPrompt.For(set, ticketText, look);
        var answer = await AskAsync(prompt, set.Key, look, agent, costTracker, cancellationToken);
        if (answer is null)
            return new SpecCutReview([], "the cut review returned nothing readable");

        var kept = new SpecCutAdmission(logger).Admit(set, answer, look);
        foreach (var finding in kept)
            logger.LogWarning("Cut review — {Phase} cannot be delivered: {Problem} — {Why}",
                finding.PhaseId, finding.Problem, finding.Why);
        return new SpecCutReview(kept);
    }

    private async Task<IReadOnlyList<CutFinding>?> AskAsync(
        string prompt, string key, DerivationLook? look, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            // Without a look the call is exactly the one it was: no cap, no tools.
            var chat = chatClientFactory.Create(
                agent, TaskType.Reasoning, look is null ? null : MaxIterations);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, key);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Plan.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Tools = DerivationTools.For(look) }, ct);
            costTracker.Track(response);
            // The answer is the last turn that says anything: with tools, earlier turns may carry
            // prose around a look, and a trailing turn may carry none.
            return SpecCutAnswerReader.Read(
                response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text ?? response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The cut review call failed");
            return null;
        }
    }
}
