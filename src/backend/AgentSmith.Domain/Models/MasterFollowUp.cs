namespace AgentSmith.Domain.Models;

/// <summary>2026-11-18-c8e2b: a follow-up spec a master proposes instead of holding its spec for it — what it would
/// make true, in one line, and why it was left out of this spec. The person decides whether it becomes work.</summary>
public sealed record MasterFollowUp(string Goal, string Why);
