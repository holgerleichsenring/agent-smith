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
