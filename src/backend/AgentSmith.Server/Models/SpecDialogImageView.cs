namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-20-3af8: one image on a conversation, as the transcript reads it — an address and a
/// kind, never the bytes.
/// <para>
/// The bytes are fetched one at a time from a route of their own because the dialog view is
/// re-read after EVERY message: a view carrying the images inline would re-send every megabyte
/// of them on every reply.
/// </para>
/// </summary>
/// <param name="At">When it was attached — what places it in a transcript rendered flat.</param>
/// <param name="Bytes">2026-10-08-e8b9g: its size; null for a legacy image not copied yet, which is not counted.</param>
/// <param name="Cited">2026-10-08-e8b9g: an approval of the conversation cites it, so it cannot be removed.</param>
public sealed record SpecDialogImageView(long Id, string MediaType, DateTimeOffset At, long? Bytes = null, bool Cited = false);
