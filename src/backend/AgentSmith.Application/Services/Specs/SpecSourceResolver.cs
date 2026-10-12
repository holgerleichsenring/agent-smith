using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the fixed precedence between the possible spec sources — the branch artifact wins,
/// then derivation. 2026-10-06-03c7d: a spec embedded in the ticket DESCRIPTION is no source any
/// more — a spec lies on the branch, and a ticket's text is input to deriving one.
/// <para>
/// 2026-09-22-6ad7: THE BRANCH IS THE ONLY SET A RUN READS. A branch that ANSWERED is the set,
/// and the approval record is not consulted, compared or merged over it. A branch with NOTHING
/// AT THE PATH is a hand-off that has not happened: the record supplies the set once and the run
/// publishes it (<see cref="ApprovedSetHandoff"/>). A branch that answered BADLY — present and
/// unreadable — is an operator's edit gone wrong, and it reaches the filed-ticket gate rather
/// than a copy. The decision also carries the revision CAUSE, because the two readers of that
/// cause are downstream of this choice.
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
    ApprovedSetHandoff handoff,
    FiledTicketSpecGate filedGate,
    ILogger<SpecSourceResolver> logger)
{
    /// <summary>What the run should work from, and whether the model has to be called.</summary>
    /// <param name="Source">Where the set came from.</param>
    /// <param name="Set">The set already available, if any.</param>
    /// <param name="NeedsModel">True when the deriver has to run — a first cut or an amendment.</param>
    /// <param name="Cause">The cause the next revision names.</param>
    /// <param name="Handback">2026-09-22-6ad7: set when the run PARKS instead of working — the
    /// approved specification is not readable on the branch and there is none to guess from.</param>
    /// <param name="Approval">2026-09-22-8b25: the approval the revision must record, set only by
    /// a DEMAND. The set a re-cut produces carries none of its own, and a re-cut published without
    /// one would read back as unapproved on the next run — losing the immunity after exactly one
    /// demand.</param>
    /// <param name="Expired">2026-10-12-3a36b: the branch's hand-back is derived again because a
    /// scoped branch got a foreign commit since it parked; a derivation that yields nothing keeps
    /// the stored hand-back rather than a fallback.</param>
    public sealed record Decision(
        SpecSource Source, SpecSet? Set, bool NeedsModel, string? Cause = null,
        SpecHandback? Handback = null, SpecApproval? Approval = null, bool Expired = false)
    {
        /// <summary>The set a derivation that yields nothing keeps instead of a fresh fallback: one over an
        /// executed head, whose record of work a fresh set would drop (2026-10-08-f114), or an expired
        /// hand-back (2026-10-12-3a36b) — expiry never approves a guess. Null when nothing is kept.</summary>
        public SpecSet? KeptOnFailure =>
            Set is { } carried && (Expired || carried.ExecutedHead.Count > 0) ? carried : null;
    }

    public Decision Decide(
        SpecSetOnBranch branch, Ticket ticket, SpecSetPointer? pointer, PipelineContext pipeline,
        string key, TicketLabelVocabulary vocabulary, SpecApprovalRecord? record = null,
        bool foreignCommitSinceHandback = false)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(branch);
        var cause = SpecRevisionCause.For(branch.Read, pointer, ticket, pipeline);
        if (SeriesShaEdges.NotCompared(branch.Read, pointer) is { Count: > 0 } unreached)
            logger.LogInformation(
                "Spec set {Key}: the recorded sha of {Repos} was not compared — this read did not reach it",
                key, string.Join(", ", unreached));
        if (branch.Read is { } artifact)
        {
            var amend = SpecAmendmentRule.NeedsModel(cause, artifact.Set, foreignCommitSinceHandback);
            var expired = foreignCommitSinceHandback && artifact.Set.IsHandedBack;
            logger.LogInformation(
                "Spec set {Key} came off the ticket branch ({Phases} phase(s)); {Mode}",
                key, artifact.Set.Phases.Count,
                amend ? "amending it with the new input" : "using it unchanged");
            return new Decision(
                SpecSource.BranchArtifact, artifact.Set, amend, Cause: cause,
                // 2026-10-08-f114: a Request changes re-derives but does not re-approve — the prior
                // approval, conversation included, rides along; an unapproved set records none.
                // 2026-10-12-3a36b: so does an expired hand-back's, onto the set derived again.
                Approval: SpecRecutDemand.ApprovalFor(cause, artifact.Set, pipeline)
                    ?? (expired || cause is SpecRevisionCause.Rework or SpecRevisionCause.StatusBack
                        ? artifact.Set.Approval : null),
                Expired: expired);
        }

        // ONLY when the path holds nothing. A branch this run could not read may carry an edit,
        // and standing a copy in front of it is how that edit disappears.
        if (branch.State == SpecSetBranchState.NothingAtThePath
            && handoff.Decide(record, key) is { } handedOver)
            return handedOver with { Cause = handedOver.Cause ?? cause };

        // A ticket the framework filed from an approved set has no spec of its own: a branch
        // without one parks rather than derive a guess.
        if (filedGate.MissingSet(ticket, new TicketKey(key), record, branch.State, vocabulary) is { } missing)
            return new Decision(SpecSource.Approved, null, false, Cause: cause, Handback: missing);

        return new Decision(SpecSource.Derived, null, NeedsModel: true, Cause: cause);
    }
}
