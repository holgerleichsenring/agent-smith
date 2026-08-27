using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-27-3eb1: caps ONE tool result on an exploring surface, with a marker saying
/// what was cut. The read tools bound their own shapes (list_files 1000 entries, grep
/// 1000 lines, directory_tree 1000 entries since this phase) but read_file may still
/// return a megabyte, and on a sweep every result is appended to the SAME message list
/// — so one oversized reply is spent for the rest of the conversation, not just once.
/// The bound is per RESULT; keeping the whole conversation inside the model's window is
/// the compaction middleware's job.
/// </summary>
public sealed class BoundedResultTool(AIFunction inner, int maxChars) : DelegatingAIFunction(inner)
{
    /// <summary>Wraps a tool so its result is truncated at the default exploring bound.</summary>
    public static AITool Wrap(AIFunction tool) =>
        new BoundedResultTool(tool, SizeLimits.ExploringToolResultMaxChars);

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await base.InvokeCoreAsync(arguments, cancellationToken);
        // AIFunctionFactory marshals a string return through JSON, so the result arrives
        // as a JsonElement, not a string — matching on `is string` alone bounded nothing.
        var text = result as string
            ?? (result is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : null);
        return text is null || text.Length <= maxChars ? result : Bound(text, maxChars);
    }

    /// <summary>Truncates to <paramref name="maxChars"/>, naming how much was dropped.</summary>
    public static string Bound(string text, int maxChars) =>
        text.Length <= maxChars
            ? text
            : text[..maxChars]
              + $"\n… [truncated: {text.Length - maxChars} of {text.Length} characters omitted "
              + "— read a narrower range, or grep for what you need]";
}
