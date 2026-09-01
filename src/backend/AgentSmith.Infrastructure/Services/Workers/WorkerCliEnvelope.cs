using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-09-01-b0d7: the structured result an agent CLI prints under
/// <c>--output-format json</c> — the answer text, whether the CLI reported an error, and
/// what the call cost by its own count.
/// <para>
/// Recognising it is not an optimisation. The envelope carries none of the fields the
/// reply parser knows (<c>text</c>, <c>tool_calls</c>, <c>error</c>), so an UNWRAPPED
/// envelope falls through to "the whole output is the assistant's text": no exception, no
/// failed run, tool calling simply stops. An operator can already trigger exactly that by
/// hand through the extra-arguments environment variable.
/// </para>
/// <para>
/// The discriminator is strict and parsed ONCE: the whole trimmed output must parse as a
/// single JSON object (which rejects trailing content) whose <c>type</c> is
/// <c>result</c>. The brace scanner is deliberately not used — the answer sits inside
/// <c>result</c> as model prose that routinely carries unbalanced braces, and the scanner
/// would mis-cut the span. Anything that is not an envelope passes through untouched.
/// </para>
/// </summary>
public sealed record WorkerCliEnvelope(
    string AnswerText,
    string? FailureReason,
    string? TerminalReason,
    int CliTurns,
    WorkerCallAccounting Accounting)
{
    public static bool TryRead(string? stdout, out WorkerCliEnvelope envelope)
    {
        envelope = null!;
        var text = (stdout ?? string.Empty).Trim();
        if (text.Length == 0 || text[0] != '{') return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (Text(root, "type") != "result") return false;
            envelope = Read(root);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static WorkerCliEnvelope Read(JsonElement root)
    {
        // Tokens come from the snake_case top-level "usage"; the model id comes from the
        // camelCase "modelUsage" keys, because the alias an agent is configured with
        // ("sonnet") is not a model the rest of the system can attribute anything to.
        var usage = Child(root, "usage");
        var turns = (int)Number(root, "num_turns");
        return new WorkerCliEnvelope(
            Text(root, "result") ?? string.Empty,
            FailureOf(root),
            Text(root, "terminal_reason"),
            turns,
            new WorkerCallAccounting(
                ModelOf(root),
                Number(usage, "input_tokens"),
                Number(usage, "output_tokens"),
                Number(usage, "cache_read_input_tokens"),
                Number(usage, "cache_creation_input_tokens"),
                Cost(root),
                turns));
    }

    /// <summary>
    /// A CLI that ran out of turns, or hit an error mid-execution, says so on
    /// <c>is_error</c> and still EXITS ZERO — so the exit code alone calls it a good call.
    /// </summary>
    private static string? FailureOf(JsonElement root)
    {
        if (!root.TryGetProperty("is_error", out var flag) || flag.ValueKind != JsonValueKind.True)
            return null;
        var terminal = Text(root, "terminal_reason");
        var said = Text(root, "result");
        return $"the worker CLI reported {Text(root, "subtype") ?? "an error"}"
            + (string.IsNullOrWhiteSpace(terminal) ? string.Empty : $" (terminal reason: {terminal})")
            + (string.IsNullOrWhiteSpace(said) ? string.Empty : $": {said}");
    }

    private static string ModelOf(JsonElement root)
    {
        var models = Child(root, "modelUsage");
        if (models.ValueKind != JsonValueKind.Object) return string.Empty;
        return string.Join("+", models.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal));
    }

    // Kind-checked accessors throughout: a field the CLI omits, or writes as null, must
    // read as absent rather than throw inside the transport.
    private static string? Text(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonElement Child(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value : default;

    private static long Number(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : 0;

    private static decimal Cost(JsonElement root) =>
        root.TryGetProperty("total_cost_usd", out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var cost) ? cost : 0m;
}
