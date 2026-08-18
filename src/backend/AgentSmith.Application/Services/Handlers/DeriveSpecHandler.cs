using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: turns any ticket into an ordered set of phase specs on the ticket branch,
/// so the `code` pipeline runs on ordinary tickets and not only on ones an operator
/// hand-wrote.
/// <para>
/// It runs after AnalyzeCode because one of its two hand-backs — "the requirement
/// contradicts what is in the repository" — is only findable once the repositories
/// have been read.
/// </para>
/// <para>
/// The accounting is the safeguard: every ticket segment is carried by a named phase
/// or discarded with a reason. If it cannot be produced, the run does NOT split at
/// all — one phase with the whole ticket, and a run event naming why. Fail safe means
/// falling back to a shape that is known to work, not partially applying one that
/// is not.
/// </para>
/// </summary>
public sealed class DeriveSpecHandler(
    ISpecSetDeriver deriver,
    ISpecSetReader reader,
    ISpecSetPublisher publisher,
    ISpecSetPointerStore pointers,
    SpecSourceResolver sourceResolver,
    SpecFallback fallback,
    SpecSetTicketCommenter commenter,
    SpecCutGate gate,
    ILogger<DeriveSpecHandler> logger)
    : ICommandHandler<DeriveSpecContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeriveSpecContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ticket is null)
            return CommandResult.Ok("Spec derivation skipped: the run has no ticket");

        var key = SpecSetKeyFactory.For(context.Ticket, context.Pipeline);
        var project = ProjectOf(context.Pipeline);
        var pointer = await pointers.GetAsync(project, key.Value, cancellationToken);
        var repo = SpecCarryingRepoResolver.Resolve(context.Repos, pointer);
        if (repo is null)
            return CommandResult.Ok("Spec derivation skipped: the run has no repo in scope");

        var segments = TicketSegmenter.Segment(context.Ticket.Description);
        context.Pipeline.Set(ContextKeys.TicketSegments, segments);

        var previous = await reader.ReadAsync(context.Pipeline, repo, key, cancellationToken);
        var cause = SpecRevisionCause.For(previous, pointer, context.Pipeline);
        var decision = sourceResolver.Decide(previous, context.Ticket, cause, key.Value);
        if (decision.Error is not null)
            return CommandResult.Fail(
                $"Ticket {context.Ticket.Id.Value} carries a malformed phase spec: {decision.Error}");

        var (set, ignored) = decision.NeedsModel
            ? await DeriveAsync(context, decision, key.Value, segments, cause, cancellationToken)
            : (decision.Set!, (IReadOnlyList<IgnoredInstruction>)[]);

        var finalized = Finalize(set, previous?.Set, cause);
        var result = await publisher.PublishAsync(
            context.Pipeline, project, repo, finalized, ignored, cancellationToken);
        // Non-blocking ratification: the cut is posted as "this is how I understood it" and
        // the run proceeds. Blocking would reintroduce exactly the wait Approval was deleted
        // for, and a correcting comment is the re-run path anyway.
        if (!finalized.IsHandedBack)
            await commenter.PostAsync(context.Pipeline, context.Tracker, finalized, cancellationToken);
        return result;
    }

    private async Task<(SpecSet Set, IReadOnlyList<IgnoredInstruction> Ignored)> DeriveAsync(
        DeriveSpecContext context, SpecSourceResolver.Decision decision, string key,
        IReadOnlyList<TicketSegment> segments, string cause, CancellationToken ct)
    {
        var (derivation, error) = await deriver.DeriveAsync(
            context.Ticket!, segments, decision.Set, cause, context.AgentConfig, context.Pipeline, ct);

        if (derivation is null)
        {
            await gate.RefusedAsync(
                context.Pipeline, context.Ticket!.Id.Value,
                $"the derivation produced nothing usable ({error})", ct);
            return (
                fallback.Build(key, context.Ticket!, segments, [], decision.Source),
                []);
        }

        // p0447: the deriver kept the least-objected cut instead of discarding it. The
        // objection is not obeyed, but it is not swallowed either — a reviewer's finding
        // that nobody can see is the same as no review.
        if (error is not null)
            await gate.KeptDespiteAsync(context.Pipeline, context.Ticket!.Id.Value, error, ct);

        if (derivation.Set.IsHandedBack || derivation.Set.Accounting.IsComplete)
            return (derivation.Set, derivation.IgnoredInstructions);

        await gate.RefusedAsync(
            context.Pipeline, context.Ticket!.Id.Value,
            "segment(s) " + string.Join(", ", derivation.Set.Accounting.Unaccounted)
            + " were neither carried by a phase nor discarded with a reason", ct);
        // The cut was refused for its COVERAGE, not for its criteria — carrying the
        // done-list forward keeps the run's acceptance contract non-empty without
        // inventing one.
        return (
            fallback.Build(
                key, context.Ticket!, segments,
                [.. derivation.Set.Phases.SelectMany(p => p.Draft.Done).Distinct(StringComparer.Ordinal)],
                decision.Source),
            derivation.IgnoredInstructions);
    }

    // The revision header is OURS, never the model's: numbering and cause are how a
    // reviewer follows the artifact, and a model free to rewrite them could erase the
    // very edit this mechanism exists to collect.
    private static SpecSet Finalize(SpecSet set, SpecSet? previous, string cause)
    {
        var history = previous?.Revisions ?? [];
        var next = new SpecRevision(history.Count + 1, cause, DateTimeOffset.UtcNow);
        return set with { Revisions = [.. history, next] };
    }

    private static string ProjectOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name)
        && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;
}
