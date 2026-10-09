namespace AgentSmith.Contracts.Sweep;

/// <summary>
/// 2026-10-08-9e6e: what one budgeted read returned. <see cref="Cut"/> is set when the budget ran out
/// with more to read; <see cref="Resume"/> is where that read continues (a page token or offset).
/// </summary>
public sealed record ChangedPage(IReadOnlyList<ChangedItem> Items, bool Cut = false, string? Resume = null);
