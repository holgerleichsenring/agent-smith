using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ee: where a push for one design conversation goes, and whether it goes at
/// all. Three channels — the outcome, the reading and the activity one — asked the same two
/// questions of a session and answered them with the same two private methods each; a rule
/// stated three times is a rule that can end up meaning three things.
/// </summary>
internal static class DialogTarget
{
    /// <summary>The dialog id a push is addressed to. The dialog id IS the thread id; a
    /// session with no thread is addressed by its channel.</summary>
    public static string Of(ConversationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return string.IsNullOrEmpty(state.ThreadId) ? state.ChannelId : state.ThreadId;
    }

    /// <summary>Whether this session is one the dashboard is holding. A chat platform shows
    /// its own delivery and expects the answer later, and its thread id is a dialog id
    /// nobody has joined.</summary>
    public static bool IsDashboard(ConversationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return string.Equals(
            state.Platform, DispatcherDefaults.PlatformDashboard, StringComparison.OrdinalIgnoreCase);
    }
}
