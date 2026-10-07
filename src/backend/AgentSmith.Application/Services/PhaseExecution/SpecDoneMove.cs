namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>2026-10-06-03c7e: what moving one spec to done/ wrote, and the planned files it leaves
/// for git to remove.</summary>
public sealed record SpecDoneMove(IReadOnlyList<string> Written, IReadOnlyList<string> Planned);
