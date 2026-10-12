using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// What the commit step does with the gate's verdict. A pass clears the findings. Inside the
/// review's fix pass any finding, or a grammar that would not load, is undone like a red build:
/// the tree returns to its verified head, so the fix pass keeps its contract. Otherwise the
/// first finding in a spec goes back to the master once — master, questions, commit — ahead of
/// the queued VerifySpec; a second one, or a grammar that would not load, fails the spec and
/// records it with every finding. 2026-10-12-fbbdb: what the spec commit has to say before the
/// verdict — a formatter that would not run — is prepended to every message.
/// </summary>
public sealed class StructureGateOutcome(SpecReviewRevert reviewRevert, ISpecProgressRecorder progress)
{
    private static readonly string[] CorrectionBlock =
        [CommandNames.AgenticMaster, CommandNames.MasterOpenQuestions, CommandNames.CommitSpecWork];

    public async Task<CommandResult> ResolveAsync(
        PipelineContext pipeline, StructureGateVerdict verdict, CancellationToken ct, string? preface = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(verdict);
        if (verdict.Passed)
        {
            pipeline.Remove(ContextKeys.StructureFindings);
            return CommandResult.Ok(Prefixed(preface, "Spec work is on the branch; structure within its Limits"));
        }
        var failure = CommandResult.Fail(Prefixed(preface, Message(verdict)));
        if (SpecReviewFixPass.InFlight(pipeline))
            return await RecordAsync(pipeline, await reviewRevert.TryRevertAsync(pipeline, failure, ct) ?? failure,
                Note(verdict), ct);
        if (verdict.Unavailable is not null || pipeline.Flag(ContextKeys.StructureGateAttempted))
            return await RecordAsync(pipeline, failure, Note(verdict), ct);
        return Correction(pipeline, verdict, preface);
    }

    private static CommandResult Correction(PipelineContext pipeline, StructureGateVerdict verdict, string? preface)
    {
        pipeline.Set(ContextKeys.StructureGateAttempted, true);
        pipeline.Set(ContextKeys.StructureFindings, verdict.Excess.Select(f => f.ToString()).ToList());
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.SpecId : null;
        return CommandResult.OkAndContinueWith(
            Prefixed(preface, $"{verdict.Excess.Count} unit(s) over their Limits; one correction pass follows"),
            [.. CorrectionBlock.Select(name => new PipelineCommand(name) { SpecId = specId })]);
    }

    private async Task<CommandResult> RecordAsync(
        PipelineContext pipeline, CommandResult result, string note, CancellationToken ct)
    {
        if (!result.IsSuccess && pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) && draft is not null)
            await progress.RecordAsync(pipeline, draft.SpecId, SpecRunState.Failed, note, cancellationToken: ct);
        return result;
    }

    private static string Prefixed(string? preface, string message) =>
        string.IsNullOrWhiteSpace(preface) ? message : $"{preface}\n{message}";

    private static string Message(StructureGateVerdict verdict) => verdict.Unavailable is { } reason
        ? $"structure gate could not judge: {reason}"
        : $"structure gate: {verdict.Excess.Count} unit(s) over their Limits\n"
          + string.Join('\n', verdict.Excess.Select(f => $"- {f}"));

    private static string Note(StructureGateVerdict verdict) => verdict.Unavailable is { } reason
        ? $"structure gate could not judge: {reason}"
        : $"structure gate: {verdict.Excess.Count} unit(s) over their Limits: "
          + string.Join("; ", verdict.Excess);
}
