using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// A DECLARED stage that ran red earns the spec's ONE repair pass, like an outstanding
/// criterion: its output is the most actionable thing the run produced. The pass is shared
/// with the account repair through SpecRepairAttempted, and the cause travels as
/// <see cref="ItemPrefix"/> on the one outstanding item, so the repair prompt can tell the
/// two apart. A resolution finding is not repaired: no stage of that repository ran.
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
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.SpecId : null;
        return CommandResult.OkAndContinueWith(
            mechanical.Message.Split('\n', 2)[0].Trim() + "; one repair pass follows",
            [.. SpecVerdict.RepairSteps(specId)]);
    }

    /// <summary>Only a stage a context declared: an inferred command is the analyzer's guess.</summary>
    private static bool Declared(PipelineContext pipeline, VerifyOutcome red) =>
        SandboxContextList.In(pipeline, red.Key).Any(context => (context.Verify ?? [])
            .Any(stage => stage.Label == red.Stage && stage.Command == red.Command));

    private static string Item(VerifyOutcome red) =>
        $"{ItemPrefix} {(string.IsNullOrEmpty(red.Key) ? "(default)" : red.Key)} {red.Stage} "
        + $"'{red.Command}' exited {red.ExitCode} (run at {red.Cwd}). Its output ends:\n"
        + VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars);
}
