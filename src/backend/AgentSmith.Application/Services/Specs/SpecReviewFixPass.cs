using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: the ONE pass the review earns, prepared and spliced.
/// <para>
/// The same shape as p0438's repair — master, questions, commit, verify — with the review
/// itself on the end, so the fix is read by the same rule that asked for it. Three things are
/// done before it is spliced, and each is a defect that would otherwise ship:
/// </para>
/// <para>
/// The account is SNAPSHOT. The fix pass runs a second VerifySpec, which writes
/// <see cref="ContextKeys.SpecAccounts"/> and the run ledger; a reverted pass that left its
/// own account behind would fail a run whose delivered state is the verified one.
/// </para>
/// <para>
/// The repair flag is SET, so the verification after the fix returns its verdict instead of
/// splicing a repair of its own inside a pass that is already the last one.
/// </para>
/// <para>
/// The outstanding criteria are CLEARED. They are set by a repair that has already run and
/// only <see cref="SpecRepairScope"/> clears them, so a fix pass would otherwise open with
/// "close exactly these, the rest is accounted for" over a list of findings that are not them.
/// </para>
/// </summary>
public static class SpecReviewFixPass
{
    /// <summary>The steps the pass repeats, stamped with the spec they belong to.</summary>
    public static IReadOnlyList<PipelineCommand> Steps(string? specId) =>
    [
        .. new[]
        {
            CommandNames.AgenticMaster,
            CommandNames.MasterOpenQuestions,
            CommandNames.CommitSpecWork,
            CommandNames.VerifySpec,
            CommandNames.ReviewSpecDiff,
        }.Select(name => new PipelineCommand(name) { SpecId = specId }),
    ];

    /// <summary>
    /// Whether this review's findings earn a pass, and what to say when they do not.
    /// <para>
    /// A pass is spliced only when EVERY sandbox has a verified head, because the undo depends
    /// on one: <see cref="VerifiedHeads"/> records none for a sandbox whose source tree was
    /// dirty at verification, and a mixed-stack run's secondary clone is never committed at
    /// all. Splicing a pass that could not be reverted would make "the revert fails the spec"
    /// a routine outcome of a run that had already delivered.
    /// </para>
    /// </summary>
    public static FixPassDecision Decide(
        PipelineContext pipeline, PhaseReviewReport report,
        IReadOnlyDictionary<string, ISandbox>? sandboxes)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(report);
        if (report.Findings.Count == 0 || InFlight(pipeline)) return new FixPassDecision(null, null);
        if (sandboxes is null) return new FixPassDecision(null, "no fix pass: no sandbox is resolvable");
        if (sandboxes.Keys.FirstOrDefault(key => VerifiedHeads.For(pipeline, key) is null) is { } without)
            return new FixPassDecision(null, $"no fix pass: {without} has no verified head");
        return new FixPassDecision(Prepare(pipeline), null);
    }

    /// <summary>Puts the pipeline into the fix pass: snapshot, flags, findings, clean slate.</summary>
    public static IReadOnlyList<PipelineCommand> Prepare(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<IReadOnlyList<SpecAccount>>(ContextKeys.SpecAccounts, out var accounts)
            && accounts is not null)
            pipeline.Set(ContextKeys.SpecAccountsSnapshot, accounts);
        pipeline.Set(ContextKeys.RunAccountsSnapshot, RunAccountLedger.Current(pipeline));
        pipeline.Set(ContextKeys.SpecReviewFixPass, true);
        pipeline.Set(ContextKeys.SpecRepairAttempted, true);
        pipeline.Remove(ContextKeys.OutstandingCriteria);
        return Steps(SpecId(pipeline));
    }

    /// <summary>True while the one fix pass is in flight.</summary>
    public static bool InFlight(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<bool>(ContextKeys.SpecReviewFixPass, out var pass) && pass;
    }

    /// <summary>Puts the account back as the FIRST verification left it.</summary>
    public static void RestoreSnapshot(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<IReadOnlyList<SpecAccount>>(ContextKeys.SpecAccountsSnapshot, out var accounts)
            && accounts is not null)
            pipeline.Set(ContextKeys.SpecAccounts, accounts);
        if (pipeline.TryGet<RunAccounts>(ContextKeys.RunAccountsSnapshot, out var run) && run is not null)
            pipeline.Set(ContextKeys.RunAccounts, run);
    }

    private static string? SpecId(PipelineContext pipeline) =>
        pipeline.TryGet<PhaseDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.PhaseId : null;
}

/// <summary>The pass to splice, or the reason there is none — never both.</summary>
public sealed record FixPassDecision(IReadOnlyList<PipelineCommand>? Steps, string? Note);
