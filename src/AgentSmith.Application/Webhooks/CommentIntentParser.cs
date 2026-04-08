using System.Text.RegularExpressions;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// Parses PR/MR comment bodies to extract agent commands.
/// Supports /agent-smith and /as prefixes, plus /approve and /reject shortcuts.
/// </summary>
public static partial class CommentIntentParser
{
    private static readonly Dictionary<string, string> PipelineAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fix"] = "fix-bug",
        ["security-scan"] = "security-scan",
        ["security"] = "security-scan",
        ["review"] = "pr-review",
    };

    public static CommentIntentType Parse(string body, out string? pipeline,
        out string? arguments, out string? dialogueComment)
    {
        pipeline = null;
        arguments = null;
        dialogueComment = null;

        if (string.IsNullOrWhiteSpace(body))
            return CommentIntentType.Unknown;

        var approveMatch = ApproveRegex().Match(body);
        if (approveMatch.Success)
        {
            var comment = approveMatch.Groups["comment"].Value.Trim();
            dialogueComment = string.IsNullOrEmpty(comment) ? null : comment;
            return CommentIntentType.DialogueApprove;
        }

        var rejectMatch = RejectRegex().Match(body);
        if (rejectMatch.Success)
        {
            var comment = rejectMatch.Groups["comment"].Value.Trim();
            dialogueComment = string.IsNullOrEmpty(comment) ? null : comment;
            return CommentIntentType.DialogueReject;
        }

        var commandMatch = CommandRegex().Match(body);
        if (commandMatch.Success)
        {
            var cmd = commandMatch.Groups["cmd"].Value.Trim();
            var args = commandMatch.Groups["args"].Value.Trim();

            if (cmd.Equals("help", StringComparison.OrdinalIgnoreCase))
                return CommentIntentType.Help;

            pipeline = PipelineAliases.TryGetValue(cmd, out var mapped) ? mapped : cmd;
            arguments = string.IsNullOrEmpty(args) ? null : args;
            return CommentIntentType.NewJob;
        }

        return CommentIntentType.Unknown;
    }

    [GeneratedRegex(@"^/(?:agent-smith|as)\s+(?<cmd>\S+)(?:\s+(?<args>.+))?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"^/approve(?:\s+(?<comment>.+))?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ApproveRegex();

    [GeneratedRegex(@"^/reject(?:\s+(?<comment>.+))?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex RejectRegex();
}
