using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Design;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ae: puts each answered design_read on the run's event stream — source, file,
/// node, and the version and last modification the response carried. Built only where the
/// pipeline holds a run id; a publish that fails is logged and never fails the read.
/// </summary>
public sealed class DesignReadRecorder(IEventPublisher events, string runId, ILogger logger)
{
    public async Task RecordAsync(string source, FigmaLink link, JsonElement nodesResponse, CancellationToken ct)
    {
        try
        {
            await events.PublishAsync(new DesignReadEvent(
                runId, source, link.ApiFileKey, link.NodeId ?? string.Empty,
                FigmaJson.Str(nodesResponse, "version") ?? string.Empty,
                FigmaJson.Str(nodesResponse, "lastModified") ?? string.Empty,
                DateTimeOffset.UtcNow), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to publish DesignRead event for run {RunId}", runId);
        }
    }
}
