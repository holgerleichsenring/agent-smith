using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Design;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ac: design_read's picture — the read node exported as PNG at the version the
/// summary was read from, handed to the shared tool-image deposit (2026-10-01-283dd), which owns
/// where and whether the model sees it. Returns the one line design_read says about it: that a
/// render follows, or why there is none — a deposit's refusal is repeated, never swallowed.
/// </summary>
public sealed class DesignNodeRender(IFigmaClient figma, IToolImageDeposit deposit)
{
    public async Task<string> RenderAsync(
        DesignSource source, FigmaLink link, JsonElement nodesResponse, CancellationToken ct)
    {
        var nodeId = link.NodeId!;
        var version = FigmaJson.Str(nodesResponse, "version");
        var scale = FigmaExportScale.For(nodesResponse, nodeId);
        var export = await figma.ExportPngAsync(source.SecretName, link.ApiFileKey, nodeId, scale, version, ct);
        if (export.Png is not { } png)
            return $"rendered image: none — {export.Failure!.KindWord}: {export.Failure.Detail}";
        var caption = $"Figma node {nodeId} of file {link.ApiFileKey}, version {version}, PNG at scale {FigmaJson.Short(scale)}";
        var result = deposit.Deposit(new ToolImage("image/png", png, caption));
        return result.IsAccepted
            ? $"rendered image: a PNG of node {nodeId} (scale {FigmaJson.Short(scale)}) follows this result"
            : $"rendered image: not shown — {result.Refusal}";
    }
}
