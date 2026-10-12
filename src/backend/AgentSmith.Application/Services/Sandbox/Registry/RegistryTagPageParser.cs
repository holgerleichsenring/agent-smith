using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: one page of a registry's <c>tags/list</c> — its tags, and the next page
/// its <c>Link: &lt;…&gt;; rel="next"</c> header names. The next page must stay on the same
/// scheme and host: a link elsewhere is not followed.
/// </summary>
public sealed class RegistryTagPageParser(ILogger<RegistryTagPageParser> logger)
{
    /// <summary>The page's tags, or null when the body is not a tag list. A null "tags" is no tags.</summary>
    public IReadOnlyList<string>? Tags(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("tags", out var tags))
                return null;
            if (tags.ValueKind != JsonValueKind.Array) return [];
            return [.. tags.EnumerateArray()
                .Where(tag => tag.ValueKind == JsonValueKind.String)
                .Select(tag => tag.GetString()!)];
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "The registry's tag list is not JSON");
            return null;
        }
    }

    public Uri? Next(Uri current, string? link)
    {
        if (string.IsNullOrEmpty(link) || !link.Contains("rel=\"next\"", StringComparison.Ordinal)) return null;
        var open = link.IndexOf('<');
        var close = link.IndexOf('>', open + 1);
        if (open < 0 || close < 0) return null;
        if (!Uri.TryCreate(current, link[(open + 1)..close], out var next)) return null;
        return next.Scheme == current.Scheme && next.Authority == current.Authority ? next : null;
    }
}
