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
/// 2026-09-17-0e79a: an APPROVED record joins above the description and below the branch — with
/// the one exception that a record approved AFTER the branch's own approval beats it
/// (<see cref="ApprovedSetSource"/>). It outranks the description because a ticket the framework
/// files stops carrying a fence, while a hand-written phase ticket has no approved record and
/// keeps the description path. The decision also carries the revision CAUSE, because the two
/// readers of that cause are downstream of this choice.
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
    ApprovedSetSource approved,
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
    public sealed record Decision(
        SpecSource Source, SpecSet? Set, bool NeedsModel, string? Error = null, string? Cause = null);

    public Decision Decide(
        SpecSetReadResult? branchArtifact, Ticket ticket, SpecSetPointer? pointer,
        PipelineContext pipeline, string key, SpecApprovalRecord? approval = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var cause = SpecRevisionCause.For(branchArtifact, pointer, ticket, pipeline);
        if (approved.Decide(approval, branchArtifact, key) is { } fromApproval) return fromApproval;

        if (branchArtifact is not null)
        {
            var amend = NeedsAmendment(cause, branchArtifact.Set);
            logger.LogInformation(
                "Spec set {Key} came off the ticket branch ({Phases} phase(s)); {Mode}",
                key, branchArtifact.Set.Phases.Count,
                amend ? "amending it with the new input" : "using it unchanged");
            return new Decision(SpecSource.BranchArtifact, branchArtifact.Set, amend, Cause: cause);
        }

        // BEFORE the description, not after it: a ticket the framework filed from an approved set
        // has no spec of its own, so a fenced block in its description is a paste, and reading it
        // would hand the run the one editable truth this phase exists to remove.
        if (filedGate.MissingSet(ticket) is { } missing)
            return new Decision(SpecSource.Approved, null, false, missing, cause);

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

    // A comment or an edited ticket is input the model has not seen, whatever the branch
    // carries — amend the SAME set rather than produce a fresh reading of the prose. A
    // reviewer's edit is already the correction and needs no model at all. A bare
    // re-trigger re-cuts a set nothing has run yet (the previous run ended before the cut
    // was worked, and a fallback cut is not meant to be permanent) but continues a set in
    // flight: an executed head is work on the branch, and the inputs that re-cut its tail
    // are read as their own causes (2026-09-08-4aa9).
    private static bool NeedsAmendment(string cause, SpecSet set) => cause switch
    {
        SpecRevisionCause.Comment or SpecRevisionCause.TicketEdit => true,
        SpecRevisionCause.Retrigger => set.Executed.Count == 0,
        _ => false,
    };
}
