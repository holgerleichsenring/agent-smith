using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: resolves the ordered set of specs a ticket's run builds, on the ticket branch. It runs after AnalyzeCode.
/// An unanswered question from the last run is pinned as the answer, and the ticket is told which reading was taken.
/// <para>
/// 2026-09-17-0e79a/b: a set a person APPROVED is never re-cut — a comment, a ticket edit or a re-trigger is recorded
/// in the revision it publishes and reported once, on the ticket and on the run. 2026-09-22-6ad7: the SET comes off
/// the ticket branch and from nowhere else; the record is resolved for the repositories it names and hands the set over
/// once, on a branch that carries nothing at the path. A filed ticket whose branch carries no readable set PARKS.
/// </para>
/// <para>2026-11-17-7c9ba: a plain re-trigger over the branch set as it is writes no revision and says nothing.</para>
/// <para>
/// 2026-11-19-6b0ba: no model cuts the ticket here. A ticket with no spec to build gets stand-ins
/// (<see cref="SpecStandIns"/>) and its coding master writes the real spec as its first work; an input over a set
/// with unbuilt specs keeps them, and the master is told the input and amends the spec itself.
/// </para>
/// </summary>
public sealed class DeriveSpecHandler(
    SeriesBranchRead branchRead,
    ISpecSetPublisher publisher,
    ISpecSetPointerStore pointers,
    ApprovedSpecSetResolver approvals,
    SpecSourceResolver sourceResolver,
    SpecStandIns standIns,
    SpecSetTicketCommenter commenter,
    ApprovedSetKeptNotice keptNotice,
    UnansweredQuestionPin questionPin,
    UnansweredQuestionNotice questionNotice, SeriesResolver seriesOf, ForeignCommitProbe expiry,
    SpecSetUnchanged unchanged)
    : ICommandHandler<DeriveSpecContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeriveSpecContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ticket is null)
            return CommandResult.Ok("Spec derivation skipped: the run has no ticket");

        var key = TicketKeyFactory.For(context.Ticket, context.Pipeline);
        var project = ProjectOf(context.Pipeline);
        var pointer = await pointers.GetAsync(project, key.Value, cancellationToken);
        var approval = await approvals.ResolveAsync(context.Pipeline, key, cancellationToken);
        if (context.Repos.Count == 0)
            return CommandResult.Ok("Spec derivation skipped: the run has no repo in scope");

        var segments = TicketSegmenter.Segment(context.Ticket.Description);
        context.Pipeline.Set(ContextKeys.TicketSegments, segments);

        // 2026-10-12-f5f7d: no repository is picked by position.
        var branch = await branchRead.ReadAsync(context.Pipeline, context.Repos, key, pointer, approval, cancellationToken);
        var onBranch = branch.OnBranch;
        var previous = onBranch.Read;
        var foreign = await expiry.ProbeAsync(context.Pipeline, context.Repos, previous?.Set, pointer, cancellationToken);
        var decision = sourceResolver.Decide(onBranch, context.Ticket, pointer, context.Pipeline, key.Value,
            Contracts.Tickets.TicketLabelVocabulary.ForOptional(context.Tracker), approval, foreign);
        if (decision.Failure is { } failed) return CommandResult.Fail(failed); // 2026-10-12-f5f7f
        if (decision.Handback is { } missing) return MissingSpecPark.Apply(context.Pipeline, missing);

        var unanswered = questionPin.Pin(previous?.Set, context.Pipeline);
        var series = seriesOf.Resolve(decision.Set, approval, pointer); // 2026-10-06-03c7c
        var set = Resolve(context, decision, key.Value, series, segments);

        // Reported BEFORE the publish, which clears the input: an unreported input is never marked as dealt with.
        var reported = await keptNotice.PostAsync(
            context.Pipeline, context.Tracker, set, decision.Cause!, decision.Discarded, cancellationToken);
        var finalized = SpecRevisionHeader.Finalize(
            set, previous, decision.Cause!, context.Ticket, decision.NeedsModel, reported, decision.Approval);
        if (unchanged.Holds(finalized, previous, decision, unanswered is not null,
                context.Pipeline.Flag(ContextKeys.SpecDerivationPreview)))
            return await publisher.KeepAsync(context.Pipeline, branch.Scope,
                finalized with { Revisions = previous!.Set.Revisions }, previous.LastCommitSha, cancellationToken);
        var result = await publisher.PublishAsync(
            context.Pipeline, project, branch.Scope, finalized, [], cancellationToken);
        if (!finalized.IsHandedBack)
            await AnnounceAsync(context, finalized, unanswered, cancellationToken);
        return result;
    }

    // Stand-ins where nothing is left to build; a set with unbuilt specs is kept, and an input over it is the master's.
    private SpecSet Resolve(
        DeriveSpecContext context, SpecSourceResolver.Decision decision, string key, string series,
        IReadOnlyList<TicketSegment> segments)
    {
        var kept = decision.Set;
        if (!decision.NeedsModel) return kept! with { Series = series };
        if (kept is { IsHandedBack: false, UnexecutedTail.Count: > 0 })
        {
            context.Pipeline.Set(ContextKeys.SpecKeptInput, decision.Cause!);
            return kept with { Series = series };
        }
        IReadOnlyList<string> scope = [.. context.Repos.Select(r => r.Name)];
        IReadOnlyList<string> taken = context.Pipeline.TryGet<IReadOnlyList<string>>(ContextKeys.SpecTakenIds, out var t)
            && t is not null ? t : [];
        var fresh = standIns.Build(key, series, context.Ticket!, segments, kept, decision.Source, scope, taken);
        return kept is null ? fresh : fresh with { Goal = kept.Goal };
    }

    // Non-blocking: the set is posted and the run proceeds. A stand-in is the ticket itself, so it is not posted.
    private async Task AnnounceAsync(
        DeriveSpecContext context, SpecSet finalized, UnansweredQuestion? unanswered, CancellationToken ct)
    {
        if (unanswered is not null)
            await questionNotice.PostAsync(context.Pipeline, context.Tracker, unanswered, ct);
        if (finalized.StandIns.Count == 0)
            await commenter.PostAsync(context.Pipeline, context.Tracker, finalized, ct);
    }

    private static string ProjectOf(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.ProjectName, out var name)
        && !string.IsNullOrWhiteSpace(name) ? name! : string.Empty;
}
