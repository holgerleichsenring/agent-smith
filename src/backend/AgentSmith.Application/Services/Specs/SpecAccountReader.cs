using System.Text.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: reads the account out of whatever the model wrote around it.
/// <para>
/// Tolerant about framing — prose, a fence — and strict about substance: an answer that
/// carries no readable rows is NOT an empty account, it is a failed one, and the caller
/// must be able to tell those apart.
/// </para>
/// </summary>
public static class SpecAccountReader
{
    public static IReadOnlyList<AccountRow>? Read(string? text)
    {
        var json = Unwrap(text);
        if (json is null) return null;
        try
        {
            // 2026-08-25-9749: read as the wire shape, which carries both the three-state
            // disposition and the two-state bool it replaced, then map to the typed row.
            return JsonSerializer.Deserialize<List<AccountRowJson>>(
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
