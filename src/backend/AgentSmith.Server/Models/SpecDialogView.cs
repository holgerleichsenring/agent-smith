namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-cb3e: everything the dashboard's dialog surface needs for one dialog id, in
/// one read: the conversation on it (null until one is opened), the projects a new one can
/// be opened on, and the sessions this caller may resume.
/// <para>
/// One payload rather than three routes because they answer one question — what is on this
/// page — and a surface that had to stitch three reads together would render three
/// different moments of the same conversation.
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
    IReadOnlyList<SpecDialogSessionSummary> OpenSessions,
    SpecDialogChannelQuestion? Question);
