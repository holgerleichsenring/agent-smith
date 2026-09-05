using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Reads the review out of whatever the model wrote around it — tolerant about framing,
/// strict about substance. An answer carrying no readable rows is a FAILED review, not an
/// empty one, and the caller must be able to tell those apart.
/// </summary>
public static class SpecReviewReader
{
    public static IReadOnlyList<CriterionReview>? Read(string? text)
    {
        var json = Unwrap(text);
        if (json is null) return null;
        try
        {
            return JsonSerializer.Deserialize<List<SpecReviewRowJson>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?.Select(row => row.ToRow()).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The outermost array: an answer may arrive fenced or framed by explanation.
    private static string? Unwrap(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }
}
