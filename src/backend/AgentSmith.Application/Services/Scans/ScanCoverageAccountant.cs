using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: accounts for every ratified scan criterion against the steps that really ran.
/// <para>
/// The account is MECHANICAL — no model is asked whether the scan scanned. The evidence
/// is the execution trail, and <see cref="CitationResolver"/> checks the claim exactly as
/// it checks a phase's: a criterion citing a command that never ran, or that ran and
/// failed, is outstanding and NAMES itself. That is the difference between a scan whose
/// dependency audit died and a scan that found nothing.
/// </para>
/// </summary>
public sealed class ScanCoverageAccountant : IScanCoverageAccountant
{
    /// <summary>A scan accounts for a target, not for a repository — p0429a reads this
    /// back to title the account it renders for a reader.</summary>
    public const string RepoKey = "scan";

    public SpecAccount Account(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var contract = pipeline.TryGet<ScanContract>(ContextKeys.ScanContract, out var c) && c is not null
            ? c
            : ScanContract.Empty;
        if (contract.Criteria.Count == 0) return new SpecAccount(RepoKey, []);

        var trail = Trail(pipeline);
        // The scan's two kinds of evidence, checked by the same resolver a phase account
        // uses: the files the scan really read, and the steps that really ran.
        var read = pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var paths) ? paths : null;
        var resolver = new CitationResolver(CitedFileIndex.FromPaths(read), Succeeded(trail));
        // 2026-08-30-03e4: the merge step's own statement about its own branch. Still
        // mechanical — a fact read off the run, exactly like the trail beside it.
        var degraded = pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out var reason)
            && !string.IsNullOrWhiteSpace(reason) ? reason : null;
        return new SpecAccount(
            RepoKey, [.. contract.Criteria.Select(c => resolver.Resolve(Row(c, trail, degraded)))]);
    }

    /// <summary>
    /// The claim before it is checked: the criterion is satisfied when its own step ran
    /// and reported success, and the step's message travels with it so a skip states its
    /// reason where the reader sees it.
    /// <para>
    /// 2026-08-30-03e4: a master step that ran green still leaves its criterion outstanding
    /// when the merge recorded a degraded triage. The step succeeded; what it was there to
    /// answer went unanswered, and the account is the place that knows.
    /// </para>
    /// </summary>
    private static AccountRow Row(
        ScanCriterion criterion, IReadOnlyList<ExecutionTrailEntry> trail, string? degradedTriage)
    {
        var entry = trail.LastOrDefault(e =>
            string.Equals(e.CommandName, criterion.AnsweredBy, StringComparison.Ordinal));
        if (entry is null)
            return new AccountRow(criterion.Statement, AccountDisposition.NotSatisfied, null,
                $"{criterion.AnsweredBy} never ran, so nothing answered this");
        if (!entry.Success)
            return new AccountRow(criterion.Statement, AccountDisposition.NotSatisfied, null,
                $"{criterion.AnsweredBy} failed: {entry.Message}");
        if (degradedTriage is not null && IsTriage(criterion))
            return new AccountRow(criterion.Statement, AccountDisposition.NotSatisfied, null,
                $"{criterion.AnsweredBy} ran but the scan was not triaged: {degradedTriage}");
        return new AccountRow(
            criterion.Statement, AccountDisposition.Satisfied, criterion.AnsweredBy, entry.Message);
    }

    private static bool IsTriage(ScanCriterion criterion) =>
        string.Equals(criterion.AnsweredBy, CommandNames.AgenticMaster, StringComparison.Ordinal);

    private static IReadOnlyList<string> Succeeded(IReadOnlyList<ExecutionTrailEntry> trail) =>
        [.. trail.Where(e => e.Success).Select(e => $"{e.CommandName}: {e.Message}")];

    private static IReadOnlyList<ExecutionTrailEntry> Trail(PipelineContext pipeline) =>
        pipeline.TryGet<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail, out var trail)
        && trail is not null
            ? trail
            : [];
}
