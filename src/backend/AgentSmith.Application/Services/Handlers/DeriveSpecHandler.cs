using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: turns any ticket into an ordered set of phase specs on the ticket branch, so
/// the `code` pipeline runs on ordinary tickets and not only on hand-written ones.
/// It runs after AnalyzeCode: the contradiction hand-back is only findable once the
/// repositories have been read. An unanswered question from the last run is pinned as
/// the answer before the model runs, and the ticket is told which reading was taken.
/// <para>
/// The accounting is the safeguard: every ticket segment is carried by a named phase
/// or discarded with a reason. If it cannot be produced, the run does NOT split at
/// all — one phase with the whole ticket, and a run event naming why. Fail safe means
/// falling back to a shape that is known to work, not partially applying one that
/// is not.
/// </para>
/// <para>
/// 2026-09-17-0e79a/b: a set a person APPROVED is never re-cut — a comment, a ticket edit or a
/// re-trigger is recorded in the revision it publishes and reported once, on the ticket and on
/// the run. 2026-09-22-6ad7: the SET comes off the ticket branch and from nowhere else; the
/// record is resolved for the repositories it names and hands the set over once, on a branch
/// that carries nothing at the path. A filed ticket whose branch carries no readable set PARKS.
/// </para>
/// </summary>
public sealed class DeriveSpecHandler(
    ISpecSetDeriver deriver,
    ISpecSetReader reader,
    ISpecSetPublisher publisher,
    ISpecSetPointerStore pointers,
    ApprovedSpecSetResolver approvals,
    SpecSourceResolver sourceResolver,
    SpecFallback fallback,
    SpecCoverageRefusal coverageRefusal,
    SpecSetTicketCommenter commenter,
    ApprovedSetKeptNotice keptNotice,
    SpecCutGate gate,
    UnansweredQuestionPin questionPin,
    UnansweredQuestionNotice questionNotice,
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
        // 2026-09-22-b6ad: the approval is resolved BEFORE the carrying repo, because filing may
        // already have written the branch and recorded which repository it wrote it into.
        var approval = await approvals.ResolveAsync(context.Pipeline, key, cancellationToken);
        var repo = SpecCarryingRepoResolver.Resolve(context.Repos, pointer, approval?.CarryingRepo);
        if (repo is null)
            return CommandResult.Ok("Spec derivation skipped: the run has no repo in scope");

        var segments = TicketSegmenter.Segment(context.Ticket.Description);
        context.Pipeline.Set(ContextKeys.TicketSegments, segments);

        var onBranch = await reader.ReadAsync(context.Pipeline, repo, key, cancellationToken);
        var previous = onBranch.Read;
        var decision = sourceResolver.Decide(
            onBranch, context.Ticket, pointer, context.Pipeline, key.Value, approval);
        if (decision.Handback is { } missing) return MissingSpecPark.Apply(context.Pipeline, missing);
        if (decision.Error is not null)
            return await gate.RefuseSpecAsync(
                context.Pipeline, context.Ticket.Id.Value, decision.Error, cancellationToken);

        var unanswered = questionPin.Pin(previous?.Set, context.Pipeline);
        var (set, ignored) = decision.NeedsModel
            ? await DeriveAsync(context, decision, key.Value, segments, cancellationToken)
            : (decision.Set!, (IReadOnlyList<IgnoredInstruction>)[]);

        // Reported BEFORE the publish, because the publish is what clears the input: a hand-back
        // must not suppress it, and an unreported input must not be marked as dealt with.
        var reported = await keptNotice.PostAsync(
            context.Pipeline, context.Tracker, set, decision.Cause!, discarded: null, cancellationToken);
        var finalized = SpecRevisionHeader.Finalize(
            set, previous, decision.Cause!, context.Ticket, decision.NeedsModel, reported);
        var result = await publisher.PublishAsync(
            context.Pipeline, project, repo, finalized, ignored, cancellationToken);
        if (!finalized.IsHandedBack)
            await AnnounceAsync(context, finalized, unanswered, cancellationToken);
        return result;
    }

    private async Task<(SpecSet Set, IReadOnlyList<IgnoredInstruction> Ignored)> DeriveAsync(
        DeriveSpecContext context, SpecSourceResolver.Decision decision, string key,
        IReadOnlyList<TicketSegment> segments, CancellationToken ct)
    {
        var (derivation, error) = await deriver.DeriveAsync(
            context.Ticket!, segments, decision.Set, decision.Cause!, context.AgentConfig, context.Pipeline, ct);

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

        return (
            await coverageRefusal.ApplyAsync(
                context.Pipeline, context.Ticket!, key, segments, derivation.Set, decision.Source, ct),
            derivation.IgnoredInstructions);
    }

    // Non-blocking ratification: the cut is posted as "this is how I understood it" and
    // the run proceeds — blocking would reintroduce the wait Approval was deleted for.
    private async Task AnnounceAsync(
        DeriveSpecContext context, SpecSet finalized, UnansweredQuestion? unanswered, CancellationToken ct)
    {
        if (unanswered is not null)
            await questionNotice.PostAsync(context.Pipeline, context.Tracker, unanswered, ct);
        await commenter.PostAsync(context.Pipeline, context.Tracker, finalized, ct);
    }

    private static string ProjectOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name)
        && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;
}
