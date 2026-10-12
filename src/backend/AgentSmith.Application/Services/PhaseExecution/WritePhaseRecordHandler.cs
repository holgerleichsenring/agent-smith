using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: dogfoods the methodology — the executed phase spec is recorded in the repository it
/// was worked in, as this project records its own.
/// <para>
/// 2026-10-06-03c7e: the record IS the spec, moved. <c>specs/done/{stem}.yaml</c> is the spec
/// plus an <c>outcome:</c> block under the stem the series already gave it — one file, no second
/// slug — and the planned files leave in the same series commit, so the directory says it ran.
/// Only the repository carrying the series gets the file and its <c>state.done</c> line. The
/// command keeps its name: it is persisted in run history.
/// </para>
/// <para>
/// 2026-10-12-f5f7d: the repository is the one the SPEC names — placed by the same rule the publish
/// placed it with, a spec naming none belonging to the series' carrying repository.
/// </para>
/// </summary>
public sealed class WritePhaseRecordHandler(
    PhaseRecordPublisher publisher,
    PhaseRecordIndexLine indexLine,
    SpecDoneRecorder recorder,
    SeriesRepos placer)
    : ICommandHandler<WritePhaseRecordContext>
{
    public async Task<CommandResult> ExecuteAsync(
        WritePhaseRecordContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // p0393: an ordinary ticket carries no phase spec — there is nothing to record.
        if (!context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) || draft is null)
            return CommandResult.Ok("No phase spec on this run; nothing to record");

        var body = PhaseRecordBody.For(draft, context.Pipeline);
        await publisher.PublishAsync(context.Pipeline, draft, body, cancellationToken);
        return await RecordAsync(context, draft, body, cancellationToken);
    }

    private async Task<CommandResult> RecordAsync(
        WritePhaseRecordContext context, PhaseDraft draft, string body, CancellationToken cancellationToken)
    {
        if (!TryPhase(context.Pipeline, draft.PhaseId, out var set, out _))
            return CommandResult.Fail($"Phase {draft.PhaseId} is in no series on this run, so nothing can carry its record");
        var carrying = CarrierName(context.Pipeline);
        var placement = placer.Place(set,
            new SeriesScope(context.Repos, SeriesScope.LegacyOf(null, carrying, context.Repos), [], carrying));
        if (placement.Refusal is { } refused)
            return CommandResult.Fail($"Phase {draft.PhaseId} ran but its record has no repository: {refused}");
        var phase = placement.Set.Phases.First(p => p.PhaseId == draft.PhaseId);
        var carrier = placement.Members.First(m => m.Name == phase.Draft.Repos[0]);

        var path = SeriesPaths.Spec(SeriesPaths.Done, phase.FileStem);
        var line = indexLine.Compose(draft.Goal, path);
        if (line is null)
            return CommandResult.Fail(
                $"Phase record pointer alone exceeds {PhaseRecordIndexLine.MaxChars} characters: {path}");
        return await recorder.RecordAsync(context.Pipeline,
            new SpecDoneRecord(carrier, placement.Set, phase, body, line, context.Repos.Count > 1), cancellationToken);
    }

    private static bool TryPhase(PipelineContext pipeline, string phaseId, out SpecSet set, out SpecPhase phase)
    {
        set = pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var s) ? s! : null!;
        phase = set?.Phases.FirstOrDefault(p => p.PhaseId == phaseId)!;
        return set is not null && phase is not null;
    }

    private static string? CarrierName(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.SpecRepo, out var name) ? name : null;
}
