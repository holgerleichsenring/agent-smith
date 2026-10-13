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
/// records it with every finding. 2026-10-12-fbbdb: what the spec commit has to say besides the
/// verdict — a formatter that would not run — is said with every message. 2026-10-13-2d0c: AFTER
/// the verdict, so a revert's reason (the first line) stays the structure finding; and a pass says
/// how much was judged, or that nothing was and why.
/// </summary>
public sealed class StructureGateOutcome(SpecReviewRevert reviewRevert, ISpecProgressRecorder progress)
{
    private static readonly string[] CorrectionBlock =
        [CommandNames.AgenticMaster, CommandNames.MasterOpenQuestions, CommandNames.CommitSpecWork];

    public async Task<CommandResult> ResolveAsync(
        PipelineContext pipeline, StructureGateVerdict verdict, string? note, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(verdict);
        if (verdict.Passed)
        {
            pipeline.Remove(ContextKeys.StructureFindings);
            return CommandResult.Ok(Noted($"Spec work is on the branch; {Judged(verdict)}", note));
        }
        var failure = CommandResult.Fail(Noted(Message(verdict), note));
        if (SpecReviewFixPass.InFlight(pipeline))
            return await RecordAsync(pipeline, await reviewRevert.TryRevertAsync(pipeline, failure, ct) ?? failure,
                Note(verdict), ct);
        if (verdict.Unavailable is not null || pipeline.Flag(ContextKeys.StructureGateAttempted))
            return await RecordAsync(pipeline, failure, Note(verdict), ct);
        return Correction(pipeline, verdict, note);
    }

    private static CommandResult Correction(PipelineContext pipeline, StructureGateVerdict verdict, string? note)
    {
        pipeline.Set(ContextKeys.StructureGateAttempted, true);
        pipeline.Set(ContextKeys.StructureFindings, verdict.Excess.Select(f => f.ToString()).ToList());
        var specId = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.SpecId : null;
        return CommandResult.OkAndContinueWith(
            Noted($"{verdict.Excess.Count} unit(s) over their Limits; one correction pass follows", note),
            [.. CorrectionBlock.Select(name => new PipelineCommand(name) { SpecId = specId })]);
    }

    private async Task<CommandResult> RecordAsync(
        PipelineContext pipeline, CommandResult result, string note, CancellationToken ct)
    {
        if (!result.IsSuccess && pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) && draft is not null)
            await progress.RecordAsync(pipeline, draft.SpecId, SpecRunState.Failed, note, cancellationToken: ct);
        return result;
    }

    private static string Noted(string message, string? note) =>
        string.IsNullOrWhiteSpace(note) ? message : $"{message}\n{note}";

    private static string Judged(StructureGateVerdict verdict)
    {
        var skipped = string.Join("; ", verdict.Skipped);
        if (verdict.JudgedContexts == 0)
            return $"structure not judged: {(skipped.Length > 0 ? skipped : "no context with Limits and a baseline at the base")}";
        var judged = $"structure judged {verdict.JudgedFiles} file(s) in {verdict.JudgedContexts} context(s), within their Limits";
        return skipped.Length > 0 ? $"{judged}; not judged: {skipped}" : judged;
    }

    private static string Message(StructureGateVerdict verdict) => verdict.Unavailable is { } reason
        ? $"structure gate could not judge: {reason}"
        : $"structure gate: {verdict.Excess.Count} unit(s) over their Limits\n"
          + string.Join('\n', verdict.Excess.Select(f => $"- {f}"));

    private static string Note(StructureGateVerdict verdict) => verdict.Unavailable is { } reason
        ? $"structure gate could not judge: {reason}"
        : $"structure gate: {verdict.Excess.Count} unit(s) over their Limits: "
          + string.Join("; ", verdict.Excess);
}
