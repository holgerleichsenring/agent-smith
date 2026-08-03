using AgentSmith.Application.Models;
using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0390: turns the ticket into a versioned work spec after AnalyzeCode and
/// before GeneratePlan, and CONTINUES it on every re-entry. Ordering the
/// requirement first is the first step of the work, not a crutch for weak models:
/// ticket prose has no arity, so the master cannot count its obligations and
/// re-proves all of them.
/// <para>
/// The spec is ADDITIONAL. The ticket stays pinned verbatim (p0357), the ledger
/// keeps seeding from the plan and the verdict keeps pairing with the p0328
/// expectation — a derivation failure therefore degrades the run, never fails it.
/// </para>
/// </summary>
public sealed class DeriveSpecificationHandler(
    IWorkSpecDeriver deriver,
    IWorkSpecReader reader,
    IWorkSpecPublisher publisher,
    IWorkSpecPointerStore pointers,
    ILogger<DeriveSpecificationHandler> logger)
    : ICommandHandler<DeriveSpecificationContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeriveSpecificationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ticket is null)
            return CommandResult.Ok("Work-spec derivation skipped: run has no ticket");

        // p0393: a ticket that already CARRIES a schema-validated phase spec has the
        // statement of what must become true. Deriving a second one from the same ticket
        // would produce a rival spec that can disagree with the validated one — and would
        // spend an LLM call to create the disagreement. p0393a unifies the two concepts;
        // until then the authored spec wins over the derived one.
        if (context.Pipeline.TryGet<Contracts.Models.PhaseDraft>(ContextKeys.PhaseSpec, out var phaseSpec)
            && phaseSpec is not null)
            return CommandResult.Ok(
                $"Work-spec derivation skipped: the ticket carries phase spec {phaseSpec.PhaseId}");

        var key = WorkSpecKeyFactory.For(context.Ticket, context.Pipeline);
        var project = ProjectOf(context.Pipeline);
        var pointer = await pointers.GetAsync(project, key.Value, cancellationToken);
        var repo = WorkSpecCarryingRepoResolver.Resolve(context.Repos, pointer);
        if (repo is null)
            return CommandResult.Ok("Work-spec derivation skipped: the run has no repo in scope");

        var previous = await reader.ReadAsync(context.Pipeline, repo, key, cancellationToken);
        var cause = WorkSpecRevisionCause.For(previous, pointer, context.Pipeline);
        var (draft, error) = await deriver.DeriveAsync(
            context.Ticket, previous?.Artifact, cause, context.AgentConfig,
            context.Pipeline, cancellationToken);
        if (draft is null)
        {
            logger.LogWarning("Work-spec derivation produced nothing usable: {Error}", error);
            return CommandResult.Ok($"No work spec derived — the run continues from the ticket ({error})");
        }

        var artifact = Finalize(draft, previous?.Artifact, cause, context.Pipeline);
        return await publisher.PublishAsync(
            context.Pipeline, project, repo, artifact, draft.IgnoredInstructions, cancellationToken);
    }

    // The revision header is OURS, never the model's: numbering and cause are how
    // a reviewer follows the artifact, and a model free to rewrite them could
    // erase the very edit this mechanism exists to collect.
    private static WorkSpecArtifact Finalize(
        WorkSpecDraft draft, WorkSpecArtifact? previous, string cause, PipelineContext pipeline)
    {
        var history = previous?.Spec.Revisions ?? [];
        var next = new WorkSpecRevision(history.Count + 1, cause, DateTimeOffset.UtcNow);
        var spec = WorkSpecDoneSection.Apply(draft.Artifact.Spec, pipeline)
            with { Revisions = [.. history, next] };
        return draft.Artifact with { Spec = spec };
    }

    private static string ProjectOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name)
        && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;
}
