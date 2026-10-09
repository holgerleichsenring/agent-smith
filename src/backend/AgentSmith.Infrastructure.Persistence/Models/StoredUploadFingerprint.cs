namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-10-08-e8b9g: one stored upload as a duplicate check compares it — its files' paths and
/// content hashes, never their bytes. A file stored before the hash column carries null.
/// </summary>
public sealed record StoredUploadFingerprint(
    string SetId, string Kind, DateTimeOffset At, IReadOnlyList<(string Path, string? Sha256)> Files);
