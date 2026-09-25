namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// How the in-thread confirmation of a proposed outcome ended (union, mirrors
/// the ScopeResolution style). Only an explicit approval files anything; an
/// explicit rejection drops the proposal and files nothing; any other reply
/// is an edit note that sends the proposal back to the master for revision.
/// </summary>
public abstract record ConfirmationResult;

/// <summary>Explicit approval — the proposal goes to the outcome sink.</summary>
public sealed record OutcomeConfirmed : ConfirmationResult;

/// <summary>Explicit rejection — nothing is filed and the thread is told so.</summary>
public sealed record OutcomeRejected : ConfirmationResult;

/// <summary>No answer inside the window — nothing is filed.</summary>
public sealed record OutcomeConfirmationTimedOut : ConfirmationResult;

/// <summary>
/// A free-text reply: the operator's edit note. The proposal is not filed;
/// the master is re-prompted with the note to revise it.
/// </summary>
public sealed record OutcomeEditRequested(string Note) : ConfirmationResult;

/// <summary>
/// 2026-09-25-8e51e: an approval of a DIFFERENT act — the proposal amends the ticket this
/// conversation belongs to instead of filing a new one.
/// <para>
/// A result of its own rather than an offered shape, because the shape ladder is fed into the
/// EDIT door: a picked shape comes back as ordinary text and becomes a note the master
/// re-proposes against, which is exactly what the ladder is for. An amendment is not a
/// re-proposal — it is the operator saying yes to this proposal, on a ticket that already exists.
/// </para>
/// </summary>
public sealed record OutcomeAmendRequested : ConfirmationResult;
