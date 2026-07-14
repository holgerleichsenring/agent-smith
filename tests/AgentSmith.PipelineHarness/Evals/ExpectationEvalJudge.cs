using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Expectations;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: the LLM judge — compares a drafted expectation against the human
/// gold PER ASSERTION with the gold block as reference. The model only maps
/// gold indices to draft indices; matched/missed/hallucinated are DERIVED
/// deterministically from that mapping (a gold without a match is missed, a
/// draft never used as a match is hallucinated), so a chatty judge cannot
/// invent categories. Scripted in the LLM-free tier, real in the eval tier.
/// </summary>
public sealed class ExpectationEvalJudge(IChatClient client)
{
    private const string SystemPrompt = """
        You judge whether a DRAFTED expectation covers a human-authored GOLD expectation.
        Both are lists of testable assertions. For every GOLD assertion decide whether some
        DRAFT assertion states the same verifiable outcome (wording may differ; the tested
        behavior must be the same). Respond with ONLY a JSON object:
        {"matches": [{"gold": <1-based gold index>, "draft": <1-based draft index>}, ...]}
        Include a pair ONLY when the draft assertion genuinely covers the gold assertion.
        """;

    public async Task<ExpectationJudgeVerdict> JudgeAsync(
        ExpectationDraft gold, ExpectationDraft draft, CancellationToken cancellationToken)
    {
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt),
             new ChatMessage(ChatRole.User, ComposeUserPrompt(gold, draft))],
            cancellationToken: cancellationToken);
        var matches = ParseMatches(response.Text ?? string.Empty);
        return BuildVerdict(gold, draft, matches);
    }

    private static string ComposeUserPrompt(ExpectationDraft gold, ExpectationDraft draft)
    {
        var sb = new StringBuilder();
        sb.AppendLine("GOLD assertions:");
        AppendNumbered(sb, gold.Expected);
        sb.AppendLine();
        sb.AppendLine("DRAFT assertions:");
        AppendNumbered(sb, draft.Expected);
        return sb.ToString();
    }

    private static void AppendNumbered(StringBuilder sb, IReadOnlyList<string> assertions)
    {
        for (var i = 0; i < assertions.Count; i++)
            sb.AppendLine($"{i + 1}. {assertions[i]}");
    }

    // Tolerant like ExpectationDraftParser: first balanced JSON object with a
    // recognisable "matches" array wins; an unparseable reply judges nothing
    // (all gold missed, all draft hallucinated) rather than throwing — the
    // report stays honest about a judge that failed to answer.
    private static Dictionary<int, int> ParseMatches(string text)
    {
        var matches = new Dictionary<int, int>();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return matches;
        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            if (!doc.RootElement.TryGetProperty("matches", out var array)
                || array.ValueKind != JsonValueKind.Array)
                return matches;
            foreach (var pair in array.EnumerateArray())
                if (pair.TryGetProperty("gold", out var g) && pair.TryGetProperty("draft", out var d))
                    matches[g.GetInt32()] = d.GetInt32();
        }
        catch (JsonException) { /* no verdict — derived as all-missed */ }
        return matches;
    }

    private static ExpectationJudgeVerdict BuildVerdict(
        ExpectationDraft gold, ExpectationDraft draft, Dictionary<int, int> matches)
    {
        var judgements = gold.Expected.Select((assertion, i) =>
            matches.TryGetValue(i + 1, out var d) && d >= 1 && d <= draft.Expected.Count
                ? new ExpectationJudgeVerdict.GoldJudgement(assertion, true, draft.Expected[d - 1])
                : new ExpectationJudgeVerdict.GoldJudgement(assertion, false, null)).ToList();
        var used = judgements.Where(j => j.Matched).Select(j => j.MatchedDraftAssertion).ToHashSet();
        var hallucinated = draft.Expected.Where(a => !used.Contains(a)).ToList();
        return new ExpectationJudgeVerdict(judgements, hallucinated);
    }
}
