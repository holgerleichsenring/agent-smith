using System.Text.Json;
using AgentSmith.Application.Models.Registry;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Parses the stager LLM's terminal response — a JSON object
/// <c>{"files":[{"path":"...","content":"..."}]}</c> — into typed
/// <see cref="StagedAuthFile"/> records. Tolerates a surrounding ```json code
/// fence. Returns an empty list (never throws) when the model produced no
/// parseable object; the caller then reports the gap loudly rather than crashing.
/// </summary>
public sealed class StagedAuthFileJsonReader
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<StagedAuthFile> Read(string? modelText)
    {
        var json = Unfence(modelText);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<StagedAuthFile>();
        try
        {
            var parsed = JsonSerializer.Deserialize<Envelope>(json, Options);
            return parsed?.Files?
                .Where(f => !string.IsNullOrWhiteSpace(f.Path) && f.Content is not null)
                .Select(f => new StagedAuthFile(f.Path!, f.Content!))
                .ToList()
                ?? (IReadOnlyList<StagedAuthFile>)Array.Empty<StagedAuthFile>();
        }
        catch (JsonException)
        {
            return Array.Empty<StagedAuthFile>();
        }
    }

    private static string Unfence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text.Trim();
    }

    private sealed record Envelope(IReadOnlyList<FileEntry>? Files);
    private sealed record FileEntry(string? Path, string? Content);
}
