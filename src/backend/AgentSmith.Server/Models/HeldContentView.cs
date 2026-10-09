namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-09-86e1: one content hash a set of the conversation holds, with that set — what the
/// selection card compares a pick against before it is sent. A file stored before the hash column
/// carries none and is never listed, so it is never matched.
/// </summary>
public sealed record HeldContentView(string Sha256, string SetId, string Name);
