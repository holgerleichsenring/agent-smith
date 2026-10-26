using System.Text.Json;
using AgentSmith.Contracts.Json;

namespace AgentSmith.Application.Services.Json;

/// <summary>
/// 2026-10-26-cb25d: reads the JSON array or object out of a model's answer, wherever in the prose it sits — the one
/// reader every answer parser maps through. A model cites ids written <c>[c2]</c> and quotes text holding braces, so
/// a scan from the first bracket to the last parses nothing: every balanced span (<see cref="JsonObjectSpans.Candidates"/>)
/// is tried in the order it opens, and the first that parses AND maps is the answer. A map that answers null passes
/// the candidate over. Null when none does — each caller decides what an unreadable answer means.
/// </summary>
public static class JsonAnswerReader
{
    /// <summary>The shape every answer prompt asks for: snake_case, which case-insensitivity alone does not bridge.
    /// One instance, so its metadata cache is built once.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>An array of objects (or an empty array), each wire row mapped.</summary>
    public static IReadOnlyList<T>? ReadArray<TWire, T>(string? text, Func<TWire, T> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return First(text, '[', ']', OpensAnAnswer,
            span => JsonSerializer.Deserialize<List<TWire>>(span, Options)?.Select(map).ToList());
    }

    /// <summary>An object read as <typeparamref name="TWire"/>; a wire the map answers null for is passed over.</summary>
    public static T? ReadObject<TWire, T>(string? text, Func<TWire, T?> map) where T : class
    {
        ArgumentNullException.ThrowIfNull(map);
        return First(text, '{', '}', null,
            span => JsonSerializer.Deserialize<TWire>(span, Options) is { } wire ? map(wire) : null);
    }

    /// <summary>An object read element by element, for wires whose names no single naming policy covers.</summary>
    public static T? ReadObject<T>(string? text, Func<JsonElement, T?> map) where T : class
    {
        ArgumentNullException.ThrowIfNull(map);
        return First(text, '{', '}', null, span =>
        {
            using var document = JsonDocument.Parse(span);
            return document.RootElement.ValueKind == JsonValueKind.Object ? map(document.RootElement) : null;
        });
    }

    private static T? First<T>(string? text, char open, char close, Func<string, bool>? opens, Func<string, T?> read)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var span in JsonObjectSpans.Candidates(text, open, close))
            if ((opens is null || opens(span)) && TryRead(span, read) is { } answer)
                return answer;
        return null;
    }

    private static T? TryRead<T>(string span, Func<string, T?> read) where T : class
    {
        try
        {
            return read(span);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // An answer array holds objects; "[c2]" or "[1, 2]" in prose is not one.
    private static bool OpensAnAnswer(string span)
    {
        var next = span.AsSpan(1).TrimStart();
        return next.Length > 0 && next[0] is '{' or ']';
    }
}
