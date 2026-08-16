using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: reads the refutations out of whatever the model wrote around them.
/// <para>
/// Tolerant about framing, strict about substance: an answer carrying no readable rows
/// is NOT "everything is substantiated" and not "everything is refuted" — it is a failed
/// call, and the caller must be able to tell that apart from a real verdict.
/// </para>
/// </summary>
public static class FindingRefutationReader
{
    public static IReadOnlyList<FindingRefutation>? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return null;
        try
        {
            return JsonSerializer.Deserialize<List<FindingRefutation>>(
                text[start..(end + 1)],
                // snake_case is what the prompt asks for, and case-insensitivity alone
                // does not bridge an underscore.
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
