using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-96aea: the one shape of a merge question (operator): its buttons "Merge it" and "Look at the pull request first",
/// a clickable link to every pull request it is about — a ticket's waiting ones, or with 'final' the series' final ones — and
/// no default, so a merge is never taken because nobody answered.
/// </summary>
public static class SeriesMergeQuestion
{
    public static SeriesQuestion Of(SeriesBeat beat, string question, string mergeOf)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var links = Links(beat, mergeOf.Trim());
        var text = links.Count == 0 ? question.Trim() : $"{question.Trim()}\n\n{string.Join("\n", links.Select(l => $"- [{l.Name}]({l.Url})"))}";
        return SeriesQuestions.Ask(text, [SeriesAskTool.MergeIt, SeriesAskTool.LookFirst], null, beat.Now, 0)
            with { NoDefault = true, MergeOf = mergeOf.Trim() }; // 2026-11-28-d1ffc: its answer names what to merge
    }

    /// <summary>2026-11-28-d1ffc: the question a delivering series asks once its final pull requests are open.</summary>
    public const string FinalText = "The series is ready. Shall I merge it, or do you want to look at the pull request first?";

    /// <summary>2026-11-28-d1ffc: what the master is told to do with an answer to a merge question.</summary>
    public static string Then(SeriesQuestion answered)
    {
        ArgumentNullException.ThrowIfNull(answered);
        if (answered.MergeOf is not { Length: > 0 } of) return string.Empty;
        var what = string.Equals(of, SeriesPullRequestTools.Final, StringComparison.OrdinalIgnoreCase) ? "the final pull requests" : $"{of}'s pull requests";
        return string.Equals(answered.Answer?.Trim(), SeriesAskTool.MergeIt, StringComparison.OrdinalIgnoreCase)
            ? $" — merge {what} with merge_pull_request (ticket '{of}')."
            : string.Equals(answered.Answer?.Trim(), SeriesAskTool.LookFirst, StringComparison.OrdinalIgnoreCase)
                ? $" — the operator looks at {what} first: merge nothing until they write that it may merge."
                : $" — about merging {what} (merge_pull_request, ticket '{of}').";
    }

    private static IReadOnlyList<(string Name, string Url)> Links(SeriesBeat beat, string mergeOf) =>
        string.Equals(mergeOf, SeriesPullRequestTools.Final, StringComparison.OrdinalIgnoreCase)
            ? [.. beat.Record.Deliveries.Select(d => ($"{d.Repo}: the final pull request", d.Url))]
            : SeriesTicketLookup.Find(beat, mergeOf) is { } fact
                ? [.. (fact.Waiting.Count > 0 ? fact.Waiting : fact.PullRequests).Select(p => ($"{fact.Ticket.Key} in {p.Repo}{(string.IsNullOrEmpty(p.Number) ? string.Empty : $" (#{p.Number})")}", p.Url))]
                : [];
}
