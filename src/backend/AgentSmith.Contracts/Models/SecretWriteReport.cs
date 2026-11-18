namespace AgentSmith.Contracts.Models;

/// <summary>2026-11-17-f3a8d: what a secret run did, in one line that never carries a value.</summary>
public sealed record SecretWriteReport(bool Written, string Line);
