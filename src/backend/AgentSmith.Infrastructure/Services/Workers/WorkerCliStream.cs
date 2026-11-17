using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-d6f5: recognises Claude Code's stream-json output — one JSON event per line, opening with a
/// <c>system</c> event and ending with the <c>result</c> event the json output prints alone. Only the first line
/// decides, so an answer that merely spans lines is never taken for a stream.
/// </summary>
internal static class WorkerCliStream
{
    /// <summary>The stream's result line as the envelope; a stream that ends without one did not answer.</summary>
    public static bool TryRead(string text, out WorkerCliEnvelope envelope)
    {
        envelope = null!;
        if (!IsStream(text)) return false;
        if (WorkerCliEnvelope.TryReadResult(LastLine(text), out envelope, SessionModel(text))) return true;
        envelope = new WorkerCliEnvelope(string.Empty, "the worker CLI's stream ended without a result", null, 0,
            new WorkerCallAccounting(string.Empty, 0, 0, 0, 0, 0m, 0));
        return true;
    }

    private static bool IsStream(string text)
    {
        var first = text.Split('\n', 2)[0].Trim();
        if (first.Length == 0 || first[0] != '{' || first.Length == text.Length) return false;
        try
        {
            using var document = JsonDocument.Parse(first);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String && type.GetString() == "system";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>2026-11-17-7c9bf: the model the init event names — the session's model. Found by its subtype,
    /// not its position: a CLI may print other system events (hooks) before it.</summary>
    private static string? SessionModel(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('{') || !trimmed.Contains("\"init\"", StringComparison.Ordinal)) continue;
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;
                if (Is(root, "type", "system") && Is(root, "subtype", "init")
                    && root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String)
                    return model.GetString();
            }
            catch (JsonException)
            {
                // A line that only mentions init is no event; the next line decides.
            }
        }
        return null;
    }

    private static bool Is(JsonElement root, string name, string value) =>
        root.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String && field.GetString() == value;

    private static string LastLine(string text) => text[(text.LastIndexOf('\n') + 1)..].Trim();
}
