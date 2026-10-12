using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// Reads agent commands out of PR/MR comment bodies in two steps, so a caller can decide
/// between them whether the comment's author may command at all.
/// <see cref="Match"/> is structural and free: <see cref="CommentCommandPrefix"/> marks a command,
/// <c>help</c> is recognised without a model, an empty tail is a bare command. <see cref="ResolveAsync"/>
/// hands the text after the prefix to <see cref="IIntentParser"/>, which is a model call, so
/// it can be free-form text in any language.
/// </summary>
public sealed class CommentIntentParser(IIntentParser intentParser)
{
    public CommentCommandMatch Match(string body)
    {
        if (CommentCommandPrefix.TailOf(body) is not { } tail)
            return CommentCommandMatch.None;
        return string.Equals(tail, "help", StringComparison.OrdinalIgnoreCase)
            ? CommentCommandMatch.Help
            : new CommentCommandMatch(CommentIntentType.NewJob, tail);
    }

    public Task<PipelineRequest> ResolveAsync(
        string tail, string configPath, CancellationToken cancellationToken) =>
        intentParser.ParseToPipelineRequestAsync(tail, configPath, cancellationToken);
}
