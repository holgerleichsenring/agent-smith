using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using static AgentSmith.Server.Services.SpecDialog.Continuation.TicketContinuationEffects;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: what approving a proposal in a ticket's OWN conversation does — the one function
/// the approval preview and the act both call, over a state read once. A bug files as it always did; a
/// ticket a person closed gets a linked follow-up; a ticket no code attempt worked takes the approved
/// series whole; a worked ticket's series is continued by spec id (<see cref="SeriesContinuationIds"/>).
/// </summary>
public static class TicketContinuation
{
    public static TicketContinuationDecision Decide(TicketContinuationState state, OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal is BugOutcome)
            return Plain(BugTicket, "A bug proposal files a fix-bug ticket, exactly as it does in any conversation.", state);
        var (drafts, error) = Drafts(proposal);
        if (error is not null) return Plain(Refused, $"The proposal cannot be recorded: {error}.", state);
        if (state.ClosedForWork) return FollowUp(state, drafts);
        return state.Worked
            ? SeriesContinuationIds.Continue(state, drafts, proposal is EpicOutcome)
            : NeverWorked(state, drafts);
    }

    private static (IReadOnlyList<SpecDraft> Drafts, string? Error) Drafts(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec => ([spec.Draft], null),
        EpicOutcome epic => new EpicChildOrderer().Order(epic.Children) is var order ? (order.Children, order.Error) : default,
        _ => ([], $"an outcome of kind '{proposal.GetType().Name}' carries no specification"),
    };

    private static TicketContinuationDecision FollowUp(TicketContinuationState state, IReadOnlyList<SpecDraft> drafts)
    {
        var executed = state.ExecutedIds.ToHashSet(StringComparer.Ordinal);
        var left = drafts.Where(d => !executed.Contains(d.SpecId)).ToList();
        var reason = left.Count == 0
            ? $"Ticket {state.TicketId} was closed by a person, and every proposed spec already ran on it — nothing is left to file."
            : $"Ticket {state.TicketId} was closed by a person, so this approval files {left.Count} spec(s) as one "
                + "follow-up ticket, and both tickets name each other.";
        return new(FollowUpClosed, reason, executed.Count, left.Count, state.BasisRunId,
            [.. drafts.Select(d => executed.Contains(d.SpecId)
                ? new TicketContinuationSpec(d.SpecId, d.Goal, SpecDispositionKinds.Kept, "ran on this ticket; not filed again")
                : new TicketContinuationSpec(d.SpecId, d.Goal, SpecDispositionKinds.Appended, "filed on the follow-up ticket"))],
            left);
    }

    // The amendment re-ids the drafts by position under the ticket's series; the preview says the same ids.
    private static TicketContinuationDecision NeverWorked(TicketContinuationState state, IReadOnlyList<SpecDraft> drafts)
    {
        var recorded = state.Record?.Set.Specs.Select(s => s.SpecId).ToList() ?? [];
        var specs = drafts.Select((d, i) => (Id: state.Series is { } series ? SeriesIdFactory.Member(series, i) : d.SpecId, d.Goal))
            .Select(d => new TicketContinuationSpec(d.Id, d.Goal,
                recorded.Contains(d.Id, StringComparer.Ordinal) ? SpecDispositionKinds.Recut : SpecDispositionKinds.Appended))
            .ToList();
        specs.AddRange(state.Record?.Set.Specs.Where(s => specs.All(p => p.SpecId != s.SpecId))
            .Select(s => new TicketContinuationSpec(s.SpecId, s.Draft.Goal, SpecDispositionKinds.Dropped)) ?? []);
        return new(AmendNeverWorked,
            $"No code attempt has worked ticket {state.TicketId}, so this approval becomes the series under it "
                + $"({drafts.Count} spec(s)), written to its branch; the description is rewritten where the tracker supports it.",
            0, drafts.Count, null, specs, drafts);
    }

    private static TicketContinuationDecision Plain(string effect, string reason, TicketContinuationState state) =>
        new(effect, reason, state.ExecutedIds.Count, 0, state.BasisRunId, [], []);
}
