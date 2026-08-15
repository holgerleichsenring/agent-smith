using System.Text;
using System.Text.Json;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0407: renders the sandbox's list_files JSON as the text the model reads. Lifted
/// out of SandboxStepRunner — running a step and formatting its output are two
/// reasons to change.
/// </summary>
internal static class DirectoryListingRenderer
{
    public static string Render(string json, bool withSizes)
    {
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                sb.AppendLine(entry.GetString());
                continue;
            }
            AppendEntry(sb, entry, withSizes);
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void AppendEntry(StringBuilder sb, JsonElement entry, bool withSizes)
    {
        var path = entry.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
        var isDir = entry.TryGetProperty("is_directory", out var d) && d.GetBoolean();
        if (withSizes)
        {
            var size = entry.TryGetProperty("size_bytes", out var s) && s.ValueKind == JsonValueKind.Number
                ? s.GetInt64().ToString().PadLeft(10) : "       DIR";
            sb.Append(size).Append("  ").Append(path);
        }
        else
        {
            sb.Append(path);
        }
        if (isDir && !path.EndsWith('/')) sb.Append('/');
        sb.AppendLine();
    }
}
