using System.Text.RegularExpressions;
using AgentSmith.Contracts.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Extracts operator answers from a comment-reply body when the parent comment
/// thread carries the leading agent-smith open-questions marker. Lines matching
/// <c>Q&lt;id&gt;: &lt;answer&gt;</c> become entries in the returned dictionary; any
/// line preceding the next anchor is appended as continuation. Comments without
/// the leading marker on the parent comment return an empty dictionary.
/// </summary>
public sealed class PlanAnswerParser
{
    // \s would consume the line break and let .* leak onto the next line; restrict
    // post-colon whitespace to horizontal spaces/tabs so each Q-line stays self-contained.
    private static readonly Regex AnswerLineRegex =
        new(@"^Q(?<id>[A-Za-z0-9_-]+):[ \t]*(?<answer>.*)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly ILogger<PlanAnswerParser>? _logger;

    public PlanAnswerParser(ILogger<PlanAnswerParser>? logger = null) => _logger = logger;

    /// <summary>
    /// Parse a reply when the parent comment is available (preferred). Returns
    /// empty when the parent isn't an agent-smith open-questions comment.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parse(string parentCommentBody, string replyBody)
    {
        if (!OpenQuestionsCommentMarkers.IsOpenQuestionsComment(parentCommentBody))
            return EmptyAnswers;
        return ExtractAnswers(replyBody);
    }

    /// <summary>
    /// Parse a single comment body that contains BOTH the marker and the answers
    /// (e.g. an operator clicked "Quote reply" so the marker quotes into the
    /// reply). Returns empty when the marker is missing or no answers parse.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parse(string commentBody)
    {
        if (!OpenQuestionsCommentMarkers.IsOpenQuestionsComment(commentBody))
            return EmptyAnswers;
        return ExtractAnswers(commentBody);
    }

    private IReadOnlyDictionary<string, string> ExtractAnswers(string replyBody)
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = AnswerLineRegex.Matches(replyBody);
        foreach (Match match in matches)
        {
            var id = match.Groups["id"].Value;
            var answer = match.Groups["answer"].Value.Trim();
            if (string.IsNullOrWhiteSpace(answer))
            {
                _logger?.LogWarning("Skipping empty answer for Q{Id}", id);
                continue;
            }
            answers[id] = answer;
        }

        if (answers.Count == 0)
            _logger?.LogWarning(
                "Open-questions reply matched the marker but contained no Q<id>: <answer> lines");

        return answers;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAnswers
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
