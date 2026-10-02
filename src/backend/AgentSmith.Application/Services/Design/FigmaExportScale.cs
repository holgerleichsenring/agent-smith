using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ac: the scale a node is exported at — its absoluteBoundingBox's longest side
/// brought to at most <see cref="MaxLongEdge"/> px, the long edge the tool-image path accepts;
/// a small node is enlarged at most <see cref="MaxScale"/>x. Floored, so rounding never pushes
/// the render over the edge. A node without bounds is exported at 1x.
/// </summary>
public static class FigmaExportScale
{
    public const int MaxLongEdge = 1568;
    public const double MaxScale = 2;
    public const double MinScale = 0.01;

    public static double For(JsonElement nodesResponse, string nodeId)
    {
        var document = FigmaJson.Obj(nodesResponse, "nodes") is { } nodes && FigmaJson.Obj(nodes, nodeId) is { } entry
            ? FigmaJson.Obj(entry, "document") : null;
        var box = document is { } d ? FigmaJson.Obj(d, "absoluteBoundingBox") : null;
        var longest = box is { } b ? Math.Max(FigmaJson.Num(b, "width") ?? 0, FigmaJson.Num(b, "height") ?? 0) : 0;
        if (longest <= 0) return 1;
        var scale = Math.Floor(MaxLongEdge / longest * 10_000) / 10_000;
        return Math.Clamp(scale, MinScale, MaxScale);
    }
}
