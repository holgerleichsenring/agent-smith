using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-6f12: the last question an account asks — the criteria no diff window settled,
/// put once more with the whole branch in reach instead of one slice of it.
/// <para>
/// A live two-repository migration was refused on two ratified criteria while all four builds
/// and test runs in both repositories exited 0. Both refusals described what the ACCOUNT
/// lacked: "the required branch-wide absence search could not run", "the API host project
/// body is not shown". One criterion spoke of hosts in both repositories and was judged inside
/// a window that held one of them, which no answer from that window could settle.
/// </para>
/// <para>
/// It runs ONCE. A second full-reach pass would be a carousel with a search bill, and the
/// account already ends a run. It can only RAISE a disposition, through
/// <see cref="AccountAnswerMerge"/>, so a criterion the windows settled is left alone.
/// </para>
/// </summary>
internal sealed class AccountFullReachPass(AccountCalls calls, ILogger logger)
{
    /// <summary>The rows this pass is for: everything the windows did not positively satisfy.
    /// A satisfied row is an answer and is never re-asked; a not-applicable one may be raised
    /// by wider reach, which is the only direction any later answer may move.</summary>
    public static IReadOnlyList<CriterionAccount> Unsettled(IReadOnlyList<CriterionAccount> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.Where(row => !row.IsSatisfied)];
    }

    public async Task<IReadOnlyList<CriterionAccount>> SettleAsync(
        IChatClient chat, string repoKey, IReadOnlyList<CriterionAccount> rows,
        AccountEvidence evidence, string diff, BranchSearch? search,
        AccountRowResolution reader, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var unsettled = Unsettled(rows);
        if (unsettled.Count == 0 || search is null)
        {
            LogSkip(repoKey, unsettled.Count, search);
            return rows;
        }

        // The allowance is opened HERE, so the windowed pass ran on its own and this pass
        // cannot arrive to find "No search left" — the message the live run was refused with.
        search.Budget.OpenNextPass();
        logger.LogInformation(
            "{Repo}: asking once more over the whole branch for {Count} criterion(s): {Criteria}",
            repoKey, unsettled.Count, string.Join(" | ", unsettled.Select(u => u.Criterion)));

        var answer = await calls.AskWithFullReachAsync(
            chat, repoKey, [.. unsettled.Select(u => u.Criterion)], evidence,
            AccountFullReachAsk.Message(unsettled, evidence.Searchable), costTracker, ct);
        // The resolver is built after the call, because this pass's own searches happen
        // during it and are the evidence its answer cites.
        var merged = AccountAnswerMerge.Of(
            rows, unsettled, answer, repoKey, reader,
            AccountTools.ResolverOver(diff, evidence.CommandResults, search));
        Report(repoKey, rows, merged);
        return merged;
    }

    /// <summary>Why it did not run. Both reasons are said out loud: a pass that is skipped
    /// silently is indistinguishable from one that ran and found nothing.</summary>
    private void LogSkip(string repoKey, int unsettled, BranchSearch? search)
    {
        if (unsettled == 0)
            logger.LogInformation(
                "{Repo}: every criterion was settled by a window — no full-reach pass", repoKey);
        else if (search is null)
            logger.LogInformation(
                "{Repo}: {Count} criterion(s) unsettled, but there is no sandbox to search — "
                + "the full-reach pass is skipped and the windows' answer stands",
                repoKey, unsettled);
    }

    /// <summary>What the pass changed, named. A mechanism that raises a verdict silently is
    /// one nobody can audit after the run, and this one is asked last.</summary>
    private void Report(
        string repoKey, IReadOnlyList<CriterionAccount> before,
        IReadOnlyList<CriterionAccount> after)
    {
        var raised = after
            .Where(row => before.Any(was =>
                string.Equals(was.Criterion, row.Criterion, StringComparison.OrdinalIgnoreCase)
                && was.Disposition < row.Disposition))
            .ToList();
        if (raised.Count == 0)
        {
            logger.LogInformation(
                "{Repo}: the full-reach pass changed nothing — the windows' answer stands", repoKey);
            return;
        }
        foreach (var row in raised)
            logger.LogInformation(
                "{Repo}: the full-reach pass settled {Criterion} as {Disposition} ({Citation})",
                repoKey, row.Criterion, row.Disposition, row.Citation);
    }
}
