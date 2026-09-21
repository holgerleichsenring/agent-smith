namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-2ba8: what the routing tag did to a freshly filed ticket — the labels the
/// resolution is then run against, and the sentence the operator reads in front of whatever the
/// start became.
/// <para>
/// THE LABELS AND THE NOTE MOVE TOGETHER, ON PURPOSE. A tag the tracker did not take must never
/// reach the envelope: the resolution would name the filing project, the starter would move the
/// ticket into a trigger status, and it would sit there reported as STARTED while no poll can
/// claim it. So the label list grows only where the write actually landed, and the note says what
/// happened either way — including nothing, which is what a project resolving by anything but a
/// tag gets.
/// </para>
/// </summary>
public sealed record FiledWorkTagging(IReadOnlyList<string> Labels, string Note)
{
    /// <summary>Nothing was written and nothing is worth saying: the labels travel unchanged.</summary>
    public static FiledWorkTagging Silent(IReadOnlyList<string> labels) => new(labels, string.Empty);

    /// <summary>
    /// Puts the tagging in front of whatever the start became. The start reasons are FRAGMENTS
    /// that follow "is started: ", so the note is one too and ends in its own separator.
    /// </summary>
    public FiledWorkStart Explaining(FiledWorkStart start) =>
        Note.Length == 0 ? start : start with { Reason = Note + start.Reason };
}
