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
/// </summary>
public sealed class ExecutedPhaseMarker(
    ISpecSetWriter specSetWriter,
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
        var write = await specSetWriter.WriteAsync(pipeline, carrier, updated, cancellationToken);
        if (!write.Written)
            logger.LogWarning(
                "Phase {PhaseId} ran but the branch could not record it as executed: {Error}",
                draft.PhaseId, write.Error);
    }

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
