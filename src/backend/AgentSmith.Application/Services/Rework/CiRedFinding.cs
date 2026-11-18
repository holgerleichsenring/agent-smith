using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-11-17-3435c: what a red CI asks of a ticket — the act (channel Ci, at the time the red was first read, or the
/// fix attempt's end), the pull requests red on their heads with their evidence, and whether the newest attempt was a
/// CI fix that left the red head where it was.
/// </summary>
public sealed record CiRedFinding(ReworkAct Act, IReadOnlyList<CiRedPullRequest> Red, bool FixPushedNothing);
