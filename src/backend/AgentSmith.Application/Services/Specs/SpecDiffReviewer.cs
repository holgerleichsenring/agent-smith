using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: asks a fresh instance what a verified spec's diff BREAKS, and keeps only
/// what rests on a file that instance read.
/// <para>
/// The same shape as the cut review — one message, no memory, tools of its own, every finding
/// checked against something the framework minted — moved from the plan to the code. What it
/// buys is the half nothing covered: the accountant asks whether the done-list is satisfied,
/// which a spec can pass while breaking every rule the repository states.
/// </para>
/// </summary>
public sealed class SpecDiffReviewer(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<SpecDiffReviewer> logger)
{
    private const string RoleName = "spec-diff-reviewer";

    /// <summary>Per call, the look allowance plus the turn that asks and the turn that
    /// answers — as <see cref="SpecCutReviewer.MaxIterations"/> pins for the cut review.</summary>
    public const int MaxIterations = DerivationLookTerms.SpecReviewAllowance + 2;

    public async Task<SpecReviewReport> ReviewAsync(
        SpecDraft draft, string? principles, IReadOnlyList<SpecDiff> diffs, DerivationLook? look,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken,
        SeriesContract? contract = null) // 2026-10-13-8762k: the set's contract, a rule source of its own
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(diffs);
        // Not "reviewed and found nothing": nobody was asked. A spec whose diff carries no
        // reviewable file has no question to put, and the record says which of the two it was.
        if (diffs.Count == 0 || diffs.All(d => d.IsEmpty))
            return SpecReviewReport.NotTaken("this spec changed no reviewable file");

        var answer = await AskAsync(
            SpecReviewPrompt.For(draft, principles, diffs, look, contract), draft.SpecId, look, agent,
            costTracker, cancellationToken);
        if (answer is null)
        {
            logger.LogWarning("The spec review of {Spec} returned nothing readable", draft.SpecId);
            return SpecReviewReport.NotTaken(
                "the review call failed or returned nothing readable");
        }

        var kept = new SpecReviewAdmission(logger).Admit(answer, diffs, look);
        foreach (var finding in kept)
            logger.LogWarning("Spec review — {Repo}/{Path}:{Line} breaks {Rule}: {Why}",
                finding.Repository, finding.Path, finding.Line, finding.Rule, finding.Why);
        return SpecReviewReport.Taken(kept);
    }

    private async Task<IReadOnlyList<SpecFinding>?> AskAsync(
        string prompt, string key, DerivationLook? look, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(
                agent, TaskType.Reasoning, look is null ? null : MaxIterations);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Verify, key);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Verify.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Tools = DerivationTools.For(look) }, ct);
            costTracker.Track(response);
            return SpecReviewAnswerReader.Read(
                response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text
                ?? response.Text);
        }
        // Guarded on the RUN's token, never on the exception's type — the LLM layer's own
        // NetworkTimeout surfaces as a TaskCanceledException with this token not cancelled,
        // and a review that fails must not fail a spec that verified green.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The spec review call failed");
            return null;
        }
    }
}
