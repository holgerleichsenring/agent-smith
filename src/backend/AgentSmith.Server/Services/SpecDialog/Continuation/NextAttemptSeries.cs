using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>A ticket's series as its next attempt will read it, over the continuation state it was read from.</summary>
/// <param name="Read">The continuation state, or why the ticket could not be read.</param>
/// <param name="Specs">The basis with the pending edit merged over it, in series order; empty on a failed read.</param>
/// <param name="Pending">The recorded edit no attempt has worked yet, or null.</param>
public sealed record NextAttemptSeries(
    TicketContinuationRead Read, IReadOnlyList<SpecDraft> Specs, SpecApprovalRecord? Pending = null);
