namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-01-283db: one uploaded website on a conversation, as the transcript shows it — a name,
/// a file count and a size, never the files.
/// </summary>
/// <param name="At">When it was uploaded — what places it in a transcript rendered flat.</param>
/// <param name="Note">2026-10-02-075dd: what the design partner recorded about the set, or null.</param>
/// <param name="Cited">2026-10-08-e8b9g: an approval of the conversation cites it, so it cannot be removed.</param>
public sealed record ReferenceSetView(
    string SetId, string Name, int Files, long Bytes, DateTimeOffset At, string? Note = null, bool Cited = false);
