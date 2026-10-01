namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-10-01-283db: one uploaded website as a conversation lists it — never its bytes.
/// <paramref name="Name"/> is the folder every path shares, or <c>site</c> when they share none.
/// </summary>
public sealed record ReferenceSetSummary(string SetId, string Name, int Files, long Bytes, DateTimeOffset At);
