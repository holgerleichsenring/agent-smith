using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: a call's input estimated from its message characters at chars/4 (a rough Latin-script approximation).
/// 2026-11-22-66e4a: only the starting point — the limiter scales it by the ratio the provider's own counts taught
/// it (<see cref="InputCostRatio"/>) and settles every call to the provider's count.
/// </summary>
internal static class MessageTokenEstimate
{
    private const int CharsPerToken = 4; // not a limit: the chars-per-token approximation of the estimate

    /// <summary>The input of <paramref name="messages"/> plus the tool definitions <paramref name="options"/> send
    /// with them (2026-11-22-66e4a: a sub-agent's first turn is mostly tool schema).</summary>
    public static int Of(IEnumerable<ChatMessage> messages, ChatOptions? options = null)
    {
        var chars = 0;
        foreach (var m in messages)
        {
            if (m.Contents is null) continue;
            foreach (var c in m.Contents)
                chars += ContentChars(c);
        }
        foreach (var tool in options?.Tools ?? [])
            chars += tool.Name.Length + tool.Description.Length
                + (tool is AIFunctionDeclaration function ? function.JsonSchema.GetRawText().Length : 0);
        return Math.Max(1, chars / CharsPerToken);
    }

    // An agentic loop re-sends the WHOLE conversation each turn, and the bulk of it is tool traffic — the model's
    // function calls and, above all, the tool RESULTS. Counting only TextContent undercounted the input ~7x on a
    // large coding run, so every content kind counts by its rendered length.
    private static int ContentChars(AIContent content) => content switch
    {
        TextContent t => t.Text?.Length ?? 0,
        FunctionCallContent call => (call.Name?.Length ?? 0) + ArgumentsLength(call.Arguments),
        FunctionResultContent result => result.Result?.ToString()?.Length ?? 0,
        _ => 0,
    };

    private static int ArgumentsLength(IDictionary<string, object?>? arguments)
    {
        if (arguments is null) return 0;
        var chars = 0;
        foreach (var (key, value) in arguments)
            chars += key.Length + (value?.ToString()?.Length ?? 0);
        return chars;
    }
}
