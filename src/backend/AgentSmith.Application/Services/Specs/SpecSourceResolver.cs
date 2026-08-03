using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the fixed precedence between the three possible spec sources — the
/// branch artifact wins, then a spec embedded in the ticket DESCRIPTION, then
/// derivation.
/// <para>
/// A ticket COMMENT is deliberately NOT a source. After the first run the ticket
/// carries the derived spec as a comment, so a rule reading "a ticket carrying a
/// spec skips derivation" would feed the run its own echo — and a comment is
/// editable by anyone, which is precisely the third-party input derivation exists
/// to bound. A comment can AMEND the set (that is the revision path); it can never
/// BE the set.
/// </para>
/// </summary>
public sealed class SpecSourceResolver(
    IPhaseSpecFromTicket specFromTicket,
    ILogger<SpecSourceResolver> logger)
{
    /// <summary>What the run should work from, and whether the model has to be called.</summary>
    /// <param name="Source">Where the set came from.</param>
    /// <param name="Set">The set already available, if any.</param>
    /// <param name="NeedsModel">True when the deriver has to run — a first cut or an amendment.</param>
    /// <param name="Error">Set when a present spec is MALFORMED, which fails loudly.</param>
    public sealed record Decision(
        SpecSource Source, SpecSet? Set, bool NeedsModel, string? Error = null);

    public Decision Decide(SpecSetReadResult? branchArtifact, Ticket ticket, string cause, string key)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (branchArtifact is not null)
        {
            // A re-trigger or a comment brings new input for the SAME set: amend it rather
            // than produce a fresh reading of the prose. A reviewer's edit is already the
            // correction — it needs no model at all.
            var amend = cause is SpecRevisionCause.Retrigger or SpecRevisionCause.Comment;
            logger.LogInformation(
                "Spec set {Key} came off the ticket branch ({Phases} phase(s)); {Mode}",
                key, branchArtifact.Set.Phases.Count,
                amend ? "amending it with the new input" : "using it unchanged");
            return new Decision(SpecSource.BranchArtifact, branchArtifact.Set, amend);
        }

        var extraction = specFromTicket.Extract(ticket.Description);
        if (extraction is PhaseSpecExtracted extracted)
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
                NeedsModel: false);
        }

        // A MALFORMED embedded spec is someone shipping a spec and getting it wrong. It
        // must not degrade silently into "no spec, derive one" — that would hide the error
        // behind a plausible derivation.
        if (extraction is PhaseSpecInvalid { IsAbsent: false } invalid)
            return new Decision(SpecSource.TicketDescription, null, false, invalid.Error);

        return new Decision(SpecSource.Derived, null, NeedsModel: true);
    }
}
