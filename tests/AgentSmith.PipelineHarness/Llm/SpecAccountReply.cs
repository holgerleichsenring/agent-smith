using System.Text;
using System.Text.Json;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0422: builds the answer an agreeing accountant would give, read off the accounting
/// prompt itself.
/// <para>
/// The delivery account is a real model call in every run. A preset whose subject is the
/// checkout, the master loop or the pull request should not have to know that — but it
/// must not silently skip it either, because in production the call happens. So the
/// scripted client answers it from what the prompt states: every criterion satisfied,
/// each citing a file the branch really changed.
/// </para>
/// </summary>
internal static class SpecAccountReply
{
    private const string CriteriaHeader = "CRITERIA";
    private const string FilesHeader = "EVERY FILE THIS BRANCH CHANGED";
    private const string AccountingMarker = "COMMANDS THAT RAN AGAINST THIS BRANCH";

    private const string CutReviewMarker = "CANNOT BE DELIVERED AS WRITTEN";

    public static bool IsAccountingCall(string prompt) =>
        prompt.Contains(AccountingMarker, StringComparison.Ordinal);

    /// <summary>The cut review runs BEFORE the work and must not consume the master's script.</summary>
    public static bool IsCutReviewCall(string prompt) =>
        prompt.Contains(CutReviewMarker, StringComparison.Ordinal);

    public static string SatisfyingEverything(string prompt)
    {
        var citation = Section(prompt, FilesHeader).FirstOrDefault() ?? "unknown";
        var rows = Section(prompt, CriteriaHeader).Select(criterion => new
        {
            criterion,
            satisfied = true,
            citation,
            note = "scripted harness account",
        });
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>
    /// 2026-08-25-7035: the mirror answer — every criterion refused. The scoring half of the
    /// account eval has to be able to produce a false NEGATIVE on demand, and an agreeing
    /// accountant can only ever produce the other kind.
    /// </summary>
    public static string RefusingEverything(string prompt)
    {
        var rows = Section(prompt, CriteriaHeader).Select(criterion => new
        {
            criterion,
            satisfied = false,
            citations = Array.Empty<string>(),
            note = "scripted harness refusal",
        });
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>The "- " items under a header, up to the next blank-line-separated header.</summary>
    private static IReadOnlyList<string> Section(string prompt, string header)
    {
        var items = new List<string>();
        var index = prompt.IndexOf(header, StringComparison.Ordinal);
        if (index < 0) return items;
        foreach (var raw in prompt[index..].Split('\n').Skip(1))
        {
            var line = raw.Trim();
            if (line.StartsWith("- ", StringComparison.Ordinal)) { items.Add(line[2..]); continue; }
            if (items.Count > 0 && line.Length == 0) break;
        }
        return items;
    }
}
