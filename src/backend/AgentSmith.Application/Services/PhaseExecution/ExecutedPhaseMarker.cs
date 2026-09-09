using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0393a: records on the BRANCH that a phase has run.
/// <para>
/// An executed phase is append-only — a later comment may re-cut the unexecuted tail but
/// never rewrite a phase whose work is already in the branch history — and the next run
/// can only honour that if the branch says which phases those are.
/// </para>
/// <para>
/// p0466: its own service. Writing the record INTO the working trees and telling the
/// branch which phases are through are two things, and the handler that does the first
/// should not also own the second.
/// </para>
/// <para>
/// 2026-09-08-4aa9: the record is this system's own commit on the spec path, so the
/// pointer moves with it. Until it did, every re-trigger after an executed phase read
/// the marker's commit as a reviewer's edit and never asked the model — a comment on
/// the ticket re-cut nothing.
/// </para>
/// </summary>
public sealed class ExecutedPhaseMarker(
    ISpecSetWriter specSetWriter,
    SpecSetPointerRecorder pointer,
    ILogger<ExecutedPhaseMarker> logger)
{
    public async Task MarkAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection>? repos,
        PhaseDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null) return;
        if (set.Executed.Contains(draft.PhaseId, StringComparer.Ordinal)) return;

        var updated = set with { Executed = [.. set.Executed, draft.PhaseId] };
        pipeline.Set(ContextKeys.SpecSet, updated);

        var carrier = CarryingRepo(pipeline, repos);
        if (carrier is null) return;
        await RecordOnBranchAsync(pipeline, carrier, updated, draft.PhaseId, cancellationToken);
    }

    private async Task RecordOnBranchAsync(
        PipelineContext pipeline, RepoConnection carrier, SpecSet updated, string phaseId,
        CancellationToken ct)
    {
        var write = await specSetWriter.WriteAsync(pipeline, carrier, updated, ct);
        if (!write.Written)
        {
            logger.LogWarning(
                "Phase {PhaseId} ran but the branch could not record it as executed: {Error}",
                phaseId, write.Error);
            return;
        }
        await pointer.RecordAsync(ProjectOf(pipeline), carrier, updated, write.CommitSha!, ct);
    }

    private static string ProjectOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name)
        && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;

    private static RepoConnection? CarryingRepo(
        PipelineContext pipeline, IReadOnlyList<RepoConnection>? repos)
    {
        if (repos is not { Count: > 0 }) return null;
        return pipeline.TryGet<string>(ContextKeys.SpecRepo, out var name)
            && !string.IsNullOrWhiteSpace(name)
                ? repos.FirstOrDefault(
                    r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) ?? repos[0]
                : repos[0];
    }
}
