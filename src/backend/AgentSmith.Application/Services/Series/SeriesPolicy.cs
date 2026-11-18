using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207b: the series' policy in the operator's words, read from the project and the dialogue
/// settings — when a ticket merges, when a waiting one starts, how often the series reports, when a question
/// defaults.</summary>
public static class SeriesPolicy
{
    public static string Merge(ResolvedProject project) => project.Merge.When switch
    {
        MergeWhen.OnGateGreen => "merge on green gate",
        MergeWhen.OnCiGreen => "merge on green CI", // 2026-11-17-0207d over 3435b
        _ => "pull requests stay open for you to merge",
    };

    public static string Start(ResolvedProject project) => project.RequiresStart == RequiresStart.OnMerge
        ? "start once the ticket they need is merged" : "start on green push";

    public static string Line(ResolvedProject project, DialogueGlobalConfig dialogue)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(dialogue);
        return $"{Merge(project)} · {Start(project)} · status every {dialogue.SeriesStatusMinutes} min · questions default "
            + $"to the recommendation after {SeriesTable.Duration(TimeSpan.FromMinutes(dialogue.QuestionDefaultMinutes))}";
    }
}
