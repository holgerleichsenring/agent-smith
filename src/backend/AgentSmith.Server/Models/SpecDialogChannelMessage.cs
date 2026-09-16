namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-9033: one spec-dialog reply as the dashboard channel delivers it — the
/// payload of the hub's "SpecDialogMessage" push. The dialog id travels with the message
/// so a page holding two dialogs open can tell them apart without trusting its own
/// subscription bookkeeping.
/// </summary>
public sealed record SpecDialogChannelMessage(
    string DialogId, string Title, string Text, DateTimeOffset At);
