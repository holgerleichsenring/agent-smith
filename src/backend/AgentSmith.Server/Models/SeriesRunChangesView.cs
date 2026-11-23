namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcj: what changed in a series' spec sets, newest first, and the tickets whose branch could not
/// be read.</summary>
public sealed record SeriesRunChangesView(IReadOnlyList<SeriesRunChangeView> Changes, IReadOnlyList<SeriesRunUnreadBranchView> Unread);
