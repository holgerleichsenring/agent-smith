using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ab: a TEXT node's characters and type — family, weight, size and line height
/// as the node's style states them. Newlines are shown escaped so one node stays one line;
/// long text is capped with its full length stated.
/// </summary>
internal static class FigmaNodeText
{
    public static string? Render(JsonElement node, int cap)
    {
        if (FigmaJson.Str(node, "characters") is not { } text) return null;
        var flat = text.Replace("\r", "").Replace("\n", "\\n").Replace("\"", "\\\"");
        var shown = flat.Length <= cap ? flat : $"{flat[..cap]}… ({text.Length} chars)";
        var line = $"text \"{shown}\"";
        if (FigmaJson.Obj(node, "style") is not { } style) return line;
        var weight = FigmaJson.Num(style, "fontWeight") is { } w ? $" {FigmaJson.Short(w)}" : "";
        var size = FigmaJson.Num(style, "fontSize") is { } s ? $" {FigmaJson.Short(s)}" : "";
        var height = FigmaJson.Num(style, "lineHeightPx") is { } h ? $"/{FigmaJson.Short(h)}" : "";
        return $"{line} {FigmaJson.Str(style, "fontFamily")}{weight}{size}{height}".TrimEnd();
    }
}
