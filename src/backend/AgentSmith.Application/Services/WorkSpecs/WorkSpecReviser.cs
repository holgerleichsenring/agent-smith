using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: applies one master-issued revision. Bound to the run by
/// <see cref="WorkSpecToolFactory"/>, so the guards can compare against the sha
/// the previous revision was written at without threading that state through the
/// tool schema.
/// </summary>
public sealed class WorkSpecReviser(
    PipelineContext pipeline,
    RepoConnection carryingRepo,
    SandboxGitOperations gitOps,
    IWorkSpecWriter writer,
    ILogger logger) : IWorkSpecReviser
{
    private string _shaAtLastRevision = string.Empty;

    public async Task<string> ReviseAsync(
        WorkSpecRevisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!pipeline.TryGet<WorkSpecArtifact>(ContextKeys.WorkSpec, out var current) || current is null)
            return "Error: this run has no work spec to revise.";
        if (WorkSpecRevisionGuards.RefuseDoneEdit(current.Spec, request.Done) is { } doneRefusal)
            return doneRefusal;

        var head = await HeadShaAsync(cancellationToken);
        var specSha = pipeline.TryGet<string>(ContextKeys.WorkSpecRevisionSha, out var s) ? s : null;
        if (WorkSpecRevisionGuards.RefuseUnproductiveRevision(
                specSha ?? string.Empty, head, _shaAtLastRevision) is { } progressRefusal)
            return progressRefusal;

        return await ApplyAsync(current, request, head, cancellationToken);
    }

    private async Task<string> ApplyAsync(
        WorkSpecArtifact current, WorkSpecRevisionRequest request, string head, CancellationToken ct)
    {
        var next = current with { Spec = Amend(current.Spec, request) };
        var result = await writer.WriteAsync(pipeline, carryingRepo, next, ct);
        if (!result.Written)
        {
            logger.LogWarning("Master work-spec revision could not be committed: {Error}", result.Error);
            return $"Error: the revision could not be committed ({result.Error}).";
        }
        pipeline.Set(ContextKeys.WorkSpec, next);
        pipeline.Set(ContextKeys.WorkSpecRevisionSha, result.CommitSha!);
        _shaAtLastRevision = head;
        logger.LogInformation(
            "Master revised the work spec to revision {Revision}: {Cause}",
            next.Spec.Current.Number, request.Cause);
        return $"Recorded revision {next.Spec.Current.Number}: {request.Cause}";
    }

    // The done-section is carried over UNCHANGED when it is read-only; the guard
    // above already refused an attempt to edit it, so this is the honest case where
    // the model simply echoed the list back.
    private static WorkSpec Amend(WorkSpec current, WorkSpecRevisionRequest request) =>
        current with
        {
            Goal = string.IsNullOrWhiteSpace(request.Goal) ? current.Goal : request.Goal,
            Requirements = request.Requirements,
            Constraints = [.. (request.Constraints ?? []).Select(r => new WorkSpecConstraint(r))],
            Assumptions = request.Assumptions ?? current.Assumptions,
            Done = current.DoneIsReadOnly ? current.Done : request.Done ?? current.Done,
            Revisions = [.. current.Revisions, new WorkSpecRevision(
                current.Revisions.Count + 1, request.Cause, DateTimeOffset.UtcNow)],
        };

    private async Task<string> HeadShaAsync(CancellationToken ct)
    {
        var matches = SandboxTargets.SandboxesForRepo(pipeline, carryingRepo);
        return matches.Count == 0 ? string.Empty : await gitOps.GetHeadCommitAsync(matches[0].Value, ct);
    }
}
