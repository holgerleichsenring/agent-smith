using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// A tool whose text result carries a "[framework] " line per sanity observation, appended at the
/// end so the head-and-tail bound keeps it. A non-text result, a result starting with "Error" and
/// a thrown exception pass through unchanged, and a result nothing flags is returned as it came —
/// the call's effect is never changed.
/// </summary>
public sealed class SanityAnnotatedTool(
    AIFunction inner, ToolResultSanity sanity, Func<ToolResultSanityScope> scope) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await base.InvokeCoreAsync(arguments, cancellationToken);
        var text = ToolResultBound.TextOf(result);
        if (text is null || ToolResultSanity.IsError(text)) return result;
        var observations = sanity.Observe(new ToolResultSanityCall(Name, arguments, text), scope());
        if (observations.Count == 0) return result;
        var lines = observations.Select(o => ToolResultSanity.Prefix + o);
        return string.Join('\n', text.Length == 0 ? lines : lines.Prepend(text));
    }
}
