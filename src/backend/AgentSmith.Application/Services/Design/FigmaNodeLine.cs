using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ab: what a builder needs from ONE Figma node, as one line — size, auto-layout,
/// fills and strokes as hex, corner radius, text and its type, the component an instance is
/// of, applied style names and bound variable names. Only values the node carries are shown;
/// nothing is inferred.
/// </summary>
internal sealed class FigmaNodeLine(
    IReadOnlyDictionary<string, string> components,
    IReadOnlyDictionary<string, string> styles,
    IReadOnlyDictionary<string, string> variables)
{
    private const int TextCap = 200;

    public string Render(JsonElement node)
    {
        var head = $"{FigmaJson.Str(node, "type")} \"{FigmaJson.Str(node, "name")}\" [{FigmaJson.Str(node, "id")}]";
        if (FigmaJson.Obj(node, "absoluteBoundingBox") is { } box)
            head += $" {FigmaJson.Short(FigmaJson.Num(box, "width") ?? 0)}x{FigmaJson.Short(FigmaJson.Num(box, "height") ?? 0)}";
        string?[] parts = [Layout(node), Paints(node, "fills", "fill"), Paints(node, "strokes", "stroke"),
            Radius(node), FigmaNodeText.Render(node, TextCap), Instance(node), Styles(node), Variables(node)];
        var attributes = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return attributes.Count == 0 ? head : $"{head} · {string.Join(" · ", attributes)}";
    }

    private static string? Layout(JsonElement node)
    {
        var mode = FigmaJson.Str(node, "layoutMode");
        if (mode is null or "NONE") return null;
        string Pad(string side) => FigmaJson.Short(FigmaJson.Num(node, $"padding{side}") ?? 0);
        return $"auto-layout {mode.ToLowerInvariant()} gap {FigmaJson.Short(FigmaJson.Num(node, "itemSpacing") ?? 0)}"
            + $" pad {Pad("Top")} {Pad("Right")} {Pad("Bottom")} {Pad("Left")}"
            + $" align {FigmaJson.Str(node, "primaryAxisAlignItems") ?? "MIN"}/{FigmaJson.Str(node, "counterAxisAlignItems") ?? "MIN"}";
    }

    private static string? Paints(JsonElement node, string member, string label)
    {
        var paints = FigmaJson.Arr(node, member)
            .Where(p => !(p.TryGetProperty("visible", out var v) && v.ValueKind == JsonValueKind.False))
            .Select(p => FigmaJson.Str(p, "type") == "SOLID" && FigmaJson.Obj(p, "color") is { } c
                ? FigmaJson.Hex(c, FigmaJson.Num(p, "opacity") ?? 1)
                : FigmaJson.Str(p, "type") ?? "?")
            .ToList();
        if (paints.Count == 0) return null;
        var weight = member == "strokes" && FigmaJson.Num(node, "strokeWeight") is { } w ? $" {FigmaJson.Short(w)}px" : "";
        return $"{label} {string.Join(",", paints)}{weight}";
    }

    private static string? Radius(JsonElement node)
    {
        var corners = FigmaJson.Arr(node, "rectangleCornerRadii")
            .Where(c => c.ValueKind == JsonValueKind.Number).Select(c => FigmaJson.Short(c.GetDouble())).ToList();
        if (corners.Count == 4 && corners.Distinct().Count() > 1) return $"radius {string.Join("/", corners)}";
        return FigmaJson.Num(node, "cornerRadius") is { } r and > 0 ? $"radius {FigmaJson.Short(r)}" : null;
    }

    private string? Instance(JsonElement node) =>
        FigmaJson.Str(node, "componentId") is { } id
            ? $"instance of \"{(components.TryGetValue(id, out var name) ? name : id)}\"" : null;

    private string? Styles(JsonElement node) => Named(
        "styles", FigmaJson.Members(FigmaJson.Obj(node, "styles"))
            .Where(m => m.Value.ValueKind == JsonValueKind.String)
            .Select(m => (m.Name, Id: m.Value.GetString()!)), styles);

    private string? Variables(JsonElement node) => Named(
        "variables", FigmaJson.Members(FigmaJson.Obj(node, "boundVariables"))
            .SelectMany(m => Aliases(m.Value).Select(id => (m.Name, Id: id))), variables);

    private static IEnumerable<string> Aliases(JsonElement value) => value.ValueKind == JsonValueKind.Array
        ? value.EnumerateArray().SelectMany(Aliases)
        : FigmaJson.Str(value, "id") is { } id ? [id] : [];

    private static string? Named(
        string label, IEnumerable<(string Name, string Id)> refs, IReadOnlyDictionary<string, string> names)
    {
        var shown = refs.Select(r => $"{r.Name}={(names.TryGetValue(r.Id, out var n) ? n : r.Id)}")
            .Distinct().ToList();
        return shown.Count == 0 ? null : $"{label} {string.Join(", ", shown)}";
    }
}
