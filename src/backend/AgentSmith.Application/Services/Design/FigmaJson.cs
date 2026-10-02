using System.Globalization;
using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ab: the small reads every Figma summary makes over a response element —
/// optional members, numbers in invariant short form, colours as hex. A member of the wrong
/// kind reads as absent, so an unexpected shape degrades to a shorter line, never a throw.
/// </summary>
internal static class FigmaJson
{
    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static double? Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public static JsonElement? Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Object ? v : null;

    public static IEnumerable<JsonElement> Arr(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    public static IEnumerable<JsonProperty> Members(JsonElement? e) =>
        e is { ValueKind: JsonValueKind.Object } o ? o.EnumerateObject() : [];

    public static string Short(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>An RGBA colour (channels 0..1) as #RRGGBB, or #RRGGBBAA when not opaque.</summary>
    public static string Hex(JsonElement color, double opacity = 1)
    {
        static int Channel(double? c) => (int)Math.Round(Math.Clamp(c ?? 0, 0, 1) * 255);
        var alpha = Channel((Num(color, "a") ?? 1) * opacity);
        var rgb = $"#{Channel(Num(color, "r")):X2}{Channel(Num(color, "g")):X2}{Channel(Num(color, "b")):X2}";
        return alpha == 255 ? rgb : $"{rgb}{alpha:X2}";
    }
}
