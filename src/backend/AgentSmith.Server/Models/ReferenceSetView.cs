namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-01-283db: one uploaded website on a conversation, as the transcript shows it — a name,
/// a file count and a size, never the files.
/// </summary>
/// <param name="At">When it was uploaded — what places it in a transcript rendered flat.</param>
public sealed record ReferenceSetView(string SetId, string Name, int Files, long Bytes, DateTimeOffset At);
