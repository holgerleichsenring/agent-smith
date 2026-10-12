using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565d: what the commit step does with the gate's verdict. A pass clears the
/// findings. The first finding in a spec goes back to the master once — master, questions,
/// commit — before VerifySpec, which stays queued. The second fails the spec and records it.
/// Inside the review's fix pass a finding is undone like a red build: the tree returns to its
/// verified head, so the fix pass keeps its contract.
/// </summary>
public sealed class StructureGateOutcome(SpecReviewRevert reviewRevert, ISpecProgressRecorder progress)
{
    public async Task<CommandResult> ResolveAsync(
        PipelineContext pipeline, StructureGateVerdict verdict, CancellationToken ct)
    {
        if (verdict.Passed)
        {
            pipeline.Remove(ContextKeys.StructureFindings);
            return CommandResult.Ok("Spec work is on the branch; structure within its Limits");
        }
        var failure = CommandResult.Fail(Message(verdict));
        if (SpecReviewFixPass.InFlight(pipeline))
            return await RecordAsync(pipeline, await reviewRevert.TryRevertAsync(pipeline, failure, ct) ?? failure, ct);
        if (verdict.Unavailable is not null || pipeline.Flag(ContextKeys.StructureGateAttempted))
            return await RecordAsync(pipeline, failure, ct);

        pipeline.Set(ContextKeys.StructureGateAttempted, true);
        pipeline.Set(ContextKeys.StructureFindings, verdict.Findings.Select(f => f.ToString()).ToList());
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.SpecId : null;
        return CommandResult.OkAndContinueWith(
            $"{verdict.Findings.Count} unit(s) over their Limits; one correction pass follows",
            [.. CorrectionBlock.Select(name => new PipelineCommand(name) { SpecId = specId })]);
    }

    /// <summary>The part of the spec block a correction repeats: work, ask, put on the branch.</summary>
    public static IReadOnlyList<string> CorrectionBlock { get; } =
        [CommandNames.AgenticMaster, CommandNames.MasterOpenQuestions, CommandNames.CommitSpecWork];

    private async Task<CommandResult> RecordAsync(PipelineContext pipeline, CommandResult result, CancellationToken ct)
    {
        if (!result.IsSuccess && pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) && draft is not null)
            await progress.RecordAsync(pipeline, draft.SpecId, SpecRunState.Failed,
                result.Message.Split('\n', 2)[0].Trim(), cancellationToken: ct);
        return result;
    }

    private static string Message(StructureGateVerdict verdict) => verdict.Unavailable is { } reason
        ? $"structure gate could not judge: {reason}"
        : $"structure gate: {verdict.Findings.Count} unit(s) over their Limits\n"
          + string.Join('\n', verdict.Findings.Select(f => $"- {f}"));
}
