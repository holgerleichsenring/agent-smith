using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207b: the series' policy in the operator's words, read from the project and the dialogue
/// settings — when a ticket merges, when a waiting one starts, when a question defaults. 2026-11-19-1b72a: the line
/// is the series page's; 2026-11-25-e94eb: no series conversation reports on a cadence.
/// 2026-11-24-f9c5c: a series with a branch merges its tickets into it under merge.series_when.</summary>
public static class SeriesPolicy
{
    public static string Merge(ResolvedProject project, string? seriesBranch = null) => project.Merge.WhenInto(seriesBranch) switch
    {
        MergeWhen.OnGateGreen => "merge on green gate",
        MergeWhen.OnCiGreen => "merge on green CI", // 2026-11-17-0207d over 3435b
        _ => "pull requests stay open for you to merge",
    };

    public static string Start(ResolvedProject project, string? seriesBranch = null) =>
        seriesBranch is not null || project.RequiresStart == RequiresStart.OnMerge
            ? "start once the ticket they need is merged" : "start on green push";

    public static string Line(ResolvedProject project, DialogueGlobalConfig dialogue, string? seriesBranch = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dialogue);
        return $"{Merge(project, seriesBranch)} · {Start(project, seriesBranch)} · questions default "
            + $"to the recommendation after {SeriesTable.Duration(TimeSpan.FromMinutes(dialogue.QuestionDefaultMinutes))}";
    }
}
