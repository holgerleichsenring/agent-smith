using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-a284: how a pull-request outcome READS — the wire word for a status, and the
/// one line a failure keeps of an exception.
/// <para>
/// Carved out of <see cref="CommitAndPRHandler"/>, which is over the file-length limit and
/// may only get shorter: this phase and the shortfall work merged into it from two sides at
/// once. Neither of these is a decision about committing or opening anything — they are how
/// the result is worded.
/// </para>
/// </summary>
internal static class PullRequestOutcomeText
{
    public static string Status(OpenStatus status) => status switch
    {
        OpenStatus.Opened => "opened",
        OpenStatus.SkippedNoChanges => "no_changes",
        _ => "failed",
    };

    /// <summary>The first line of a message, bounded — a stack trace is not a reason.</summary>
    public static string FirstLine(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length > 160 ? line[..160] : line;
    }
}
