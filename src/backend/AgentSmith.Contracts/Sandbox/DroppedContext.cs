namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0336: a repo or a single context within a kept repo that scoping excluded
/// from the run's footprint, with the reason — so the operator SEES why a whole
/// sandbox was shed. A null <see cref="Context"/> means the entire repo was
/// dropped; a set value means one context inside an otherwise-kept repo.
/// </summary>
public sealed record DroppedContext(string Repo, string? Context, string Reason);
