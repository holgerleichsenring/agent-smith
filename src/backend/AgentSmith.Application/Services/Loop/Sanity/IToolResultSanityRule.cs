using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// One mechanical shape of a suspicious tool result. A rule names at most one observation per
/// call — the fact and one thing to do — and never blocks, retries or rewrites the call.
/// </summary>
public interface IToolResultSanityRule
{
    /// <summary>The observation for this call, without the framework prefix; null when nothing is flagged.</summary>
    string? Observe(ToolResultSanityCall call, ToolResultSanityScope scope);
}

/// <summary>One finished tool call: the tool's name, the arguments the model passed, the result text.</summary>
public sealed record ToolResultSanityCall(string Tool, AIFunctionArguments Arguments, string Result)
{
    /// <summary>A string argument, whether it arrived as a string or a JSON value; null when absent.</summary>
    public string? Argument(string name) =>
        Arguments.TryGetValue(name, out var value) ? value switch
        {
            null => null,
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            _ => value.ToString(),
        } : null;

    /// <summary>A boolean argument; false when absent or not a true value.</summary>
    public bool Flag(string name) =>
        Arguments.TryGetValue(name, out var value) && value switch
        {
            bool flag => flag,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false,
        };
}
