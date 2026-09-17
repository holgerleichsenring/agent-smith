namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: one of the caller's conversations, as the dashboard lists it — open or
/// closed, addressed by its session id, because a dialog id is only the tab it was last on.
/// </summary>
/// <param name="Title">The first line the person wrote, or null when they have not written one.</param>
/// <param name="Outcome">What the latest filing created, or null when nothing was filed.</param>
/// <param name="OpenDialogId">The dialog id an OPEN conversation lives on, or null when it is
/// closed. An open conversation is already somewhere, so the page goes there instead of resuming
/// it — a resume is refused while its turn runs, and a person who left mid-turn could otherwise
/// not get back to the reply, or to an approval that is waiting for them.</param>
public sealed record SpecDialogSessionSummary(
    string SessionId, string Project, int Turns, DateTimeOffset LastActivityAt,
    string? Title, SpecDialogConversationOutcome? Outcome, string? OpenDialogId);
