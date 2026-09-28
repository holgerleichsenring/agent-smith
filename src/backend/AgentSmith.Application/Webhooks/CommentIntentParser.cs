using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// Reads agent commands out of PR/MR comment bodies in two steps, so a caller can decide
/// between them whether the comment's author may command at all.
/// <see cref="Match"/> is structural and free: the <c>/agent-smith</c> or <c>/as</c> prefix
/// marks a command, <c>help</c> is recognised without a model. <see cref="ResolveAsync"/>
/// hands the text after the prefix to <see cref="IIntentParser"/>, which is a model call, so
/// it can be free-form text in any language.
/// </summary>
public sealed partial class CommentIntentParser(IIntentParser intentParser)
{
    public CommentCommandMatch Match(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return CommentCommandMatch.None;

        var commandMatch = CommandRegex().Match(body);
        if (!commandMatch.Success)
            return CommentCommandMatch.None;

        var tail = commandMatch.Groups["tail"].Value.Trim();
        return string.Equals(tail, "help", StringComparison.OrdinalIgnoreCase)
            ? CommentCommandMatch.Help
            : new CommentCommandMatch(CommentIntentType.NewJob, tail);
    }

    public Task<PipelineRequest> ResolveAsync(
        string tail, string configPath, CancellationToken cancellationToken) =>
        intentParser.ParseToPipelineRequestAsync(tail, configPath, cancellationToken);

    // Slash-prefix discrimination is intentionally a regex: operators expect a
    // deterministic "this is a command" marker. The tail capture group passes
    // everything after the prefix through to the model verbatim.
    [GeneratedRegex(@"^/(?:agent-smith|as)\s+(?<tail>.+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CommandRegex();
}
