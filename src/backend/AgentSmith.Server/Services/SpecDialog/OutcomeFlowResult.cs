namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315c: what the outcome flow tells the router. Completed covers every
/// terminal path (answer, filed, rejected, timed out — all already reported
/// in-thread); an edit request asks the router to re-run the design turn over
/// the updated transcript, which now ends with the operator's edit note.
/// </summary>
public abstract record OutcomeFlowResult;

/// <summary>The outcome flow is done; the turn ends here.</summary>
public sealed record OutcomeFlowCompleted : OutcomeFlowResult;

/// <summary>The operator asked for changes — re-run the turn with the note.</summary>
public sealed record OutcomeFlowEditRequested(string Note) : OutcomeFlowResult;
