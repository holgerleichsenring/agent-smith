namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: everything the dashboard's dialog surface needs for one dialog id, in
/// one read: the conversation on it (null until one is opened) and the projects a new one can
/// be opened on.
/// <para>
/// The caller's other conversations are NOT here. The page reads this view after every hub
/// message, and a list that reads every listed transcript for its titles would be reloaded
/// on every reply; it has its own read.
/// </para>
/// </summary>
/// <param name="Question">The question the turn is blocked on, if it is. It reaches the
/// page as a hub push and lived nowhere else, so a reload during the fifteen-minute
/// approval gate left a blocked master and a page with no question and no button — on the
/// one gate that files real tickets.</param>
public sealed record SpecDialogView(
    string DialogId,
    SpecDialogSessionView? Session,
    IReadOnlyList<SpecDialogProjectView> Projects,
    SpecDialogChannelQuestion? Question);
