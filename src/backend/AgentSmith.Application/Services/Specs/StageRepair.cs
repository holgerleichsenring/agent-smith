using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// A stage that ran red goes back to the spec's master with its output — the most actionable thing the run
/// produced. 2026-11-18-c8e2c: the only repair there is; the master hands it to its child — 2026-11-18-c8e2d: a
/// declared stage or an inferred one alike. The cause travels as <see cref="ItemPrefix"/> on the one outstanding
/// item; the first verdict before a repair is kept, so a line the repair does not restate keeps its disposition.
/// A resolution finding is not repaired: no stage of that repository ran.
/// <para>
/// 2026-11-18-9c1cc: not once — while the repairs make progress, read mechanically as the re-engagement loop reads
/// it: the repair pass wrote to the tree (its write log is not empty) and the failure changed. The same red again,
/// a repair that wrote nothing, or a spent cost budget ends it; the executor's step budget is the backstop.
/// </para>
/// </summary>
public static class StageRepair
{
    public const string ItemPrefix = "A declared gate stage ran red:";

    /// <summary>The repair continuation, or null when a finding stands, no ran stage exited non-zero, the budget is
    /// spent, or the last repair made no progress.</summary>
    public static CommandResult? Offer(
        PipelineContext pipeline, IReadOnlyList<VerifyOutcome> ran, IReadOnlyList<string> findings,
        CommandResult mechanical)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (findings.Count > 0) return null;
        if (ran.FirstOrDefault(o => o.ExitCode != 0) is not { } red) return null;
        if (PipelineCostTracker.GetOrCreate(pipeline).IsBudgetExhausted) return null;
        var signature = Signature(red);
        if (pipeline.TryGet<string>(ContextKeys.SpecRepairRed, out var last) && last is not null
            && !MadeProgress(pipeline, last, signature)) return null;

        pipeline.Set(ContextKeys.SpecRepairRed, signature);
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { Item(red) });
        if (!pipeline.Has(ContextKeys.MasterVerificationBeforeRepair)
            && pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var before) && before is not null)
            pipeline.Set(ContextKeys.MasterVerificationBeforeRepair, before);
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.SpecId : null;
        return CommandResult.OkAndContinueWith(
            mechanical.Message.Split('\n', 2)[0].Trim() + "; it goes back to the master with its output",
            [.. RepairBlock.Select(name => new PipelineCommand(name) { SpecId = specId })]);
    }

    /// <summary>
    /// The part of the spec block a repair repeats: work, anything it needs to ask, put on the branch, verified
    /// again — each stamped with the spec it repairs (p0341g). p0449: the question step belongs to it.
    /// </summary>
    public static IReadOnlyList<string> RepairBlock { get; } =
    [
        CommandNames.AgenticMaster,
        CommandNames.MasterOpenQuestions,
        CommandNames.CommitSpecWork,
        CommandNames.VerifySpec,
    ];

    // The repair pass's own write log (a master step's file host is per step) and the failure it left.
    private static bool MadeProgress(PipelineContext pipeline, string lastRed, string red) =>
        !string.Equals(lastRed, red, StringComparison.Ordinal)
        && pipeline.TryGet<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, out var wrote)
        && ReengageProgressPolicy.ProducedNewWork([], wrote ?? []);

    private static string Signature(VerifyOutcome red) =>
        $"{red.Key}|{red.Stage}|{red.ExitCode}|{VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars)}";

    private static string Item(VerifyOutcome red) =>
        $"{ItemPrefix} {(string.IsNullOrEmpty(red.Key) ? "(default)" : red.Key)} {red.Stage} "
        + $"'{red.Command}' exited {red.ExitCode} (run at {red.Cwd}). Its output ends:\n"
        + VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars);
}
