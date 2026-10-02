namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-02-0d72: what a website upload answers — the stored set as the transcript shows it, and
/// the files left out of it because they are not site files: the first
/// <see cref="Services.References.ReferenceUploadLimits.MaxSkippedListed"/> paths and how many in all.
/// </summary>
public sealed record ReferenceUploadView(
    string SetId, string Name, int Files, long Bytes, DateTimeOffset At,
    IReadOnlyList<string> Skipped, int SkippedCount);
