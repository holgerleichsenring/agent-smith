using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-11-17-3435c: a fix run's read of the red CI it serves, beside the review read in FetchTicket — the failed checks
/// into <see cref="ContextKeys.CiFailures"/> for the master's prompt, the act into the run's act slot (newest wins). When
/// the act the run serves is the CI's and merge.ci_fix_attempts fix runs in a row have not turned it green, or the last
/// fix left the red head unmoved, the run hands back with the evidence instead of trying again: answers true, and the
/// caller splices the hand-back step in, so the park ends the run before a sandbox exists.
/// </summary>
public sealed class CiRework(IServiceScopeFactory scopes)
{
    public async Task<bool> ApplyAsync(FetchTicketContext context, CancellationToken ct)
    {
        var pipeline = context.Pipeline;
        if (context.TicketId is not { } ticket || !pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
            || project is not { Merge.When: MergeWhen.OnCiGreen }
            || !pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var previous) || previous is null) return false;
        IReadOnlyList<PrReviewRecordView> records;
        using (var scope = scopes.CreateScope())
            records = scope.ServiceProvider.GetService<IPrReviewRecordReader>() is { } reader
                ? await reader.ByTicketAsync(project.Name, ticket.Value, ct) : [];
        if (CiAct.Of(project, previous, records) is not { } finding) return false;
        var feedback = CiEvidenceText.Feedback(finding);
        pipeline.Set(ContextKeys.CiFailures, feedback);
        ReworkActReader.ApplyPullRequest(pipeline, finding.Act);
        if (!pipeline.TryGet<ReworkAct>(ContextKeys.ReworkAct, out var act) || act?.Channel != ReworkChannel.Ci) return false;
        if (!finding.FixPushedNothing && previous.CiFixStreak < project.Merge.CiFixAttempts) return false;
        pipeline.Set(ContextKeys.SpecHandback, new SpecHandback(SpecHandbackCase.CiFixBudgetSpent,
            $"{Why(finding, previous, project.Merge.CiFixAttempts)}\n\n{CiEvidenceText.Of(feedback)}"));
        return true;
    }

    private static string Why(CiRedFinding finding, PreviousAttempt previous, int budget) => finding.FixPushedNothing
        ? $"The last CI fix run ({previous.RunId}) left the red head where it was, so the same checks fail again."
        : $"The CI is still red after {previous.CiFixStreak} fix run(s) in a row (merge.ci_fix_attempts: {budget}).";
}
