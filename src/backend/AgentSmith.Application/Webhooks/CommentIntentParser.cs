using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// Parses PR/MR comment bodies to extract agent commands.
/// Supports /agent-smith and /as prefixes, plus /approve and /reject shortcuts.
///
/// p0146e: the slash-prefix regexes are kept — they are the deterministic UI contract
/// operators rely on ("this is a command, not chatter"). What used to be a hardcoded
/// PipelineAliases table plus cmd/args split is now delegated to <see cref="IIntentParser"/>,
/// so the body after the slash prefix can be free-form text in any language (e.g.
/// "/agent-smith fixe den Bug von gestern" routes to the fix-bug pipeline).
/// /help, /approve, /reject stay structural — no LLM call.
/// </summary>
public sealed partial class CommentIntentParser(IIntentParser intentParser)
{
    public async Task<ParsedCommentIntent> ParseAsync(
        string body, string configPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body))
            return ParsedCommentIntent.Unknown();

        var approveMatch = ApproveRegex().Match(body);
        if (approveMatch.Success)
        {
            var comment = approveMatch.Groups["comment"].Value.Trim();
            return ParsedCommentIntent.Approve(string.IsNullOrEmpty(comment) ? null : comment);
        }

        var rejectMatch = RejectRegex().Match(body);
        if (rejectMatch.Success)
        {
            var comment = rejectMatch.Groups["comment"].Value.Trim();
            return ParsedCommentIntent.Reject(string.IsNullOrEmpty(comment) ? null : comment);
        }

        var commandMatch = CommandRegex().Match(body);
        if (commandMatch.Success)
        {
            var tail = commandMatch.Groups["tail"].Value.Trim();

            // /help and /agent-smith help are structural — no LLM call needed.
            if (string.Equals(tail, "help", StringComparison.OrdinalIgnoreCase))
                return ParsedCommentIntent.Help();

            // Everything after the slash prefix flows through IIntentParser. The LLM
            // resolves "fixe einen Bug" → fix-bug, "security review" → security-scan,
            // and extracts a ticket id when present.
            var request = await intentParser.ParseToPipelineRequestAsync(
                tail, configPath, cancellationToken);

            return ParsedCommentIntent.NewJob(request);
        }

        return ParsedCommentIntent.Unknown();
    }

    // Slash-prefix discrimination is intentionally a regex: operators expect a
    // deterministic "this is a command" marker. The tail capture group passes
    // everything after the prefix through to the LLM verbatim.
    [GeneratedRegex(@"^/(?:agent-smith|as)\s+(?<tail>.+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"^/approve(?:\s+(?<comment>.+))?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ApproveRegex();

    [GeneratedRegex(@"^/reject(?:\s+(?<comment>.+))?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex RejectRegex();
}

/// <summary>
/// Typed result of <see cref="CommentIntentParser.ParseAsync"/>. Carries the LLM-resolved
/// PipelineRequest for NewJob, the dialogue comment text for Approve/Reject, or nothing
/// for Help/Unknown.
/// </summary>
public sealed record ParsedCommentIntent(
    CommentIntentType Type,
    PipelineRequest? Request = null,
    string? DialogueComment = null)
{
    public static ParsedCommentIntent NewJob(PipelineRequest request) =>
        new(CommentIntentType.NewJob, Request: request);

    public static ParsedCommentIntent Help() => new(CommentIntentType.Help);

    public static ParsedCommentIntent Approve(string? comment) =>
        new(CommentIntentType.DialogueApprove, DialogueComment: comment);

    public static ParsedCommentIntent Reject(string? comment) =>
        new(CommentIntentType.DialogueReject, DialogueComment: comment);

    public static ParsedCommentIntent Unknown() => new(CommentIntentType.Unknown);
}
