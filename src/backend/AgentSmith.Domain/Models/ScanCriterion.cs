namespace AgentSmith.Domain.Models;

/// <summary>
/// p0429: one thing a scan says it is looking for, and the step that answers it.
/// <para>
/// A scan without a stated target has no "satisfied" and no way to tell a MISS from a
/// NON-GOAL: a dependency audit that failed to restore made the run report clean.
/// <see cref="AnsweredBy"/> is a command name, so the answer is read off the execution
/// trail rather than asked of a model.
/// </para>
/// </summary>
public sealed record ScanCriterion(string Statement, string AnsweredBy);
