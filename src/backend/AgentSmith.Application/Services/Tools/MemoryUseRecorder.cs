using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-25-9324b: puts what a master said about its memories on the run's event stream, where the ledger's
/// projection writes it — the stream is the only DB channel a spawned orchestrator has. Built per master start
/// where the pipeline holds a run id; a publish that fails is logged and answered false, never thrown.
/// </summary>
public sealed class MemoryUseRecorder(
    IEventPublisher publisher, string runId, string? specId, string project, string ticketId, ILogger logger)
{
    public Task<bool> RecordReliedAsync(IReadOnlyList<MemoryCitation> relied, CancellationToken ct) =>
        PublishAsync(relied, null, null, ct);

    public Task<bool> RecordDisputeAsync(MemoryCitation disputed, string reason, CancellationToken ct) =>
        PublishAsync([], disputed, reason, ct);

    private async Task<bool> PublishAsync(
        IReadOnlyList<MemoryCitation> relied, MemoryCitation? disputed, string? reason, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(new MemoryUseRecordedEvent(
                runId, DateTimeOffset.UtcNow, specId, project, ticketId, relied, disputed, reason), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to publish MemoryUseRecorded for run {RunId}", runId);
            return false;
        }
    }
}
