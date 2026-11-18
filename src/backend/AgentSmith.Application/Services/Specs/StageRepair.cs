using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// A DECLARED stage that ran red goes back to the spec's master ONCE with its output — the most
/// actionable thing the run produced. 2026-11-18-c8e2c: the only repair there is; the master hands it
/// to its child. The cause travels as <see cref="ItemPrefix"/> on the one outstanding item; the
/// verdict before the repair is kept, so a line the repair does not restate keeps its disposition.
/// A resolution finding is not repaired: no stage of that repository ran.
/// </summary>
public static class StageRepair
{
    public const string ItemPrefix = "A declared gate stage ran red:";

    /// <summary>The repair continuation, or null when a finding stands, the repair is spent,
    /// or no ran stage exited non-zero.</summary>
    public static CommandResult? Offer(
        PipelineContext pipeline, IReadOnlyList<VerifyOutcome> ran, IReadOnlyList<string> findings,
        CommandResult mechanical)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (findings.Count > 0) return null;
        if (pipeline.TryGet<bool>(ContextKeys.SpecRepairAttempted, out var tried) && tried) return null;
        if (ran.FirstOrDefault(o => o.ExitCode != 0) is not { } red || !Declared(pipeline, red)) return null;

        pipeline.Set(ContextKeys.SpecRepairAttempted, true);
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { Item(red) });
        if (pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var before) && before is not null)
            pipeline.Set(ContextKeys.MasterVerificationBeforeRepair, before);
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.SpecId : null;
        return CommandResult.OkAndContinueWith(
            mechanical.Message.Split('\n', 2)[0].Trim() + "; one repair pass follows",
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

    /// <summary>Only a stage a context declared: an inferred command is the analyzer's guess.</summary>
    private static bool Declared(PipelineContext pipeline, VerifyOutcome red) =>
        SandboxContextList.In(pipeline, red.Key).Any(context => (context.Verify ?? [])
            .Any(stage => stage.Label == red.Stage && stage.Command == red.Command));

    private static string Item(VerifyOutcome red) =>
        $"{ItemPrefix} {(string.IsNullOrEmpty(red.Key) ? "(default)" : red.Key)} {red.Stage} "
        + $"'{red.Command}' exited {red.ExitCode} (run at {red.Cwd}). Its output ends:\n"
        + VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars);
}
