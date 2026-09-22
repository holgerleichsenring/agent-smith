using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the fixed precedence between the possible spec sources — the branch artifact wins,
/// then a spec embedded in the ticket DESCRIPTION, then derivation.
/// <para>
/// 2026-09-22-6ad7: THE BRANCH IS THE ONLY SET A RUN READS. A branch that ANSWERED is the set,
/// and the approval record is not consulted, compared or merged over it. A branch with NOTHING
/// AT THE PATH is a hand-off that has not happened: the record supplies the set once and the run
/// publishes it (<see cref="ApprovedSetHandoff"/>). A branch that answered BADLY — present and
/// unreadable — is an operator's edit gone wrong, and it reaches the filed-ticket gate rather
/// than a copy. The record outranks the description either way, because a ticket the framework
/// files stops carrying a fence, while a hand-written phase ticket has no record and keeps the
/// description path. The decision also carries the revision CAUSE, because the two readers of
/// that cause are downstream of this choice.
/// </para>
/// <para>
/// A ticket COMMENT is deliberately NOT a source. After the first run the ticket carries the
/// derived spec as a comment, so a rule reading "a ticket carrying a spec skips derivation"
/// would feed the run its own echo — and a comment is editable by anyone, which is precisely the
/// third-party input derivation exists to bound. A comment can AMEND the set (that is the
/// revision path); it can never BE the set.
/// </para>
/// </summary>
public sealed class SpecSourceResolver(
    IPhaseSpecFromTicket specFromTicket,
    ApprovedSetHandoff handoff,
    FiledTicketSpecGate filedGate,
    ILogger<SpecSourceResolver> logger)
{
    /// <summary>What the run should work from, and whether the model has to be called.</summary>
    /// <param name="Source">Where the set came from.</param>
    /// <param name="Set">The set already available, if any.</param>
    /// <param name="NeedsModel">True when the deriver has to run — a first cut or an amendment.</param>
    /// <param name="Error">Set when a present spec is MALFORMED or a required one is MISSING,
    /// which fails loudly.</param>
    /// <param name="Cause">The cause the next revision names.</param>
    /// <param name="Handback">2026-09-22-6ad7: set when the run PARKS instead of working — the
    /// approved specification is not readable on the branch and there is none to guess from.</param>
    public sealed record Decision(
        SpecSource Source, SpecSet? Set, bool NeedsModel, string? Error = null, string? Cause = null,
        SpecHandback? Handback = null);

    public Decision Decide(
        SpecSetOnBranch branch, Ticket ticket, SpecSetPointer? pointer,
        PipelineContext pipeline, string key, SpecApprovalRecord? record = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(branch);
        var cause = SpecRevisionCause.For(branch.Read, pointer, ticket, pipeline);
        if (branch.Read is { } artifact)
        {
            var amend = SpecAmendmentRule.NeedsModel(cause, artifact.Set);
            logger.LogInformation(
                "Spec set {Key} came off the ticket branch ({Phases} phase(s)); {Mode}",
                key, artifact.Set.Phases.Count,
                amend ? "amending it with the new input" : "using it unchanged");
            return new Decision(SpecSource.BranchArtifact, artifact.Set, amend, Cause: cause);
        }

        // ONLY when the path holds nothing. A branch this run could not read may carry an edit,
        // and standing a copy in front of it is how that edit disappears.
        if (branch.State == SpecSetBranchState.NothingAtThePath
            && handoff.Decide(record, key) is { } handedOver)
            return handedOver with { Cause = handedOver.Cause ?? cause };

        // BEFORE the description, not after it: a ticket the framework filed from an approved set
        // has no spec of its own, so a fenced block in its description is a paste, and reading it
        // would hand the run the one editable truth this phase exists to remove.
        if (filedGate.MissingSet(ticket, new SpecSetKey(key), record, branch.State) is { } missing)
            return new Decision(SpecSource.Approved, null, false, Cause: cause, Handback: missing);

        var extraction = specFromTicket.Extract(ticket.Description);
        if (extraction is PhaseSpecExtracted extracted) return FromDescription(extracted, ticket, key, cause);

        // A MALFORMED embedded spec is someone shipping a spec and getting it wrong. It
        // must not degrade silently into "no spec, derive one" — that would hide the error
        // behind a plausible derivation.
        if (extraction is PhaseSpecInvalid { IsAbsent: false } invalid)
            return new Decision(SpecSource.TicketDescription, null, false, invalid.Error, cause);

        return new Decision(SpecSource.Derived, null, NeedsModel: true, Cause: cause);
    }

    private Decision FromDescription(PhaseSpecExtracted extracted, Ticket ticket, string key, string cause)
    {
        logger.LogInformation(
            "Ticket {Ticket} carries phase spec {PhaseId} in its DESCRIPTION — no derivation",
            ticket.Id.Value, extracted.Draft.PhaseId);
        var phase = new SpecPhase(
            extracted.Draft, PhaseIdFactory.Slug(extracted.Draft.Goal), string.Empty, []);
        return new Decision(
            SpecSource.TicketDescription,
            new SpecSet(
                key, [phase], SpecAccounting.Empty,
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
                SpecSource.TicketDescription),
            NeedsModel: false,
            Cause: cause);
    }
}
