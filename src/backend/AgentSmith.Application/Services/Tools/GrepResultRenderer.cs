using System.Text;
using System.Text.Json;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0407: renders the sandbox's grep JSON as the text the model reads, per output
/// mode, including the truncation notice. Lifted out of SandboxStepRunner.
/// </summary>
internal static class GrepResultRenderer
{
    public static string Render(string json, GrepOutputMode mode, int headLimit)
    {
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.EnumerateArray().ToList();
        var sb = new StringBuilder();
        switch (mode)
        {
            case GrepOutputMode.FilesWithMatches:
                foreach (var r in rows) sb.AppendLine(r.GetProperty("path").GetString());
                if (rows.Count >= headLimit) sb.AppendLine($"(truncated: {headLimit} files)");
                break;
            case GrepOutputMode.Count:
                foreach (var r in rows)
                    sb.AppendLine($"{r.GetProperty("path").GetString()}: {r.GetProperty("count").GetInt32()}");
                if (rows.Count >= headLimit) sb.AppendLine($"(truncated: {headLimit} files)");
                break;
            default:
                AppendContent(sb, rows, headLimit);
                break;
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void AppendContent(StringBuilder sb, List<JsonElement> rows, int headLimit)
    {
        var matchCount = 0;
        foreach (var r in rows)
        {
            var kind = r.TryGetProperty("kind", out var k) ? k.GetString() : "match";
            var sep = kind == "context" ? '-' : ':';
            sb.Append(r.GetProperty("path").GetString()).Append(sep)
              .Append(r.GetProperty("line").GetInt32()).Append(sep)
              .AppendLine(r.GetProperty("text").GetString());
            if (kind == "match") matchCount++;
        }
        if (matchCount >= headLimit) sb.AppendLine($"(truncated: {headLimit} matches)");
    }
}
