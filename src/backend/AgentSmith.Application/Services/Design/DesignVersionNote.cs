using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ae: the line a design_read opens with when the caller names the version a
/// ticket cites. A moved design is REPORTED, not resolved: the line states both versions, and
/// whether to build the cited or the current one is the reader's call. Empty when no version
/// is cited.
/// </summary>
public static class DesignVersionNote
{
    public static string Render(string? expectedVersion, bool readCited, JsonElement nodesResponse)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion))
            return string.Empty;
        var expected = expectedVersion.Trim();
        if (readCited)
            return $"reading the cited version {expected}, not the current one\n";
        var current = FigmaJson.Str(nodesResponse, "version");
        return current == expected
            ? $"design unchanged: version {expected} is current\n"
            : $"design moved: {expected} -> {current} ({FigmaJson.Str(nodesResponse, "lastModified")}); this is the "
              + "current version — call again with read_version true to read the cited one\n";
    }
}
