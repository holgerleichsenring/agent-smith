using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// 2026-09-17-042eh: the phase's review findings, to the SERVER, as JSON.
/// <para>
/// The record body states them in prose for whoever opens the phase; that copy is markdown
/// inside a YAML body and cannot be read back as data without parsing it apart again. This
/// event carries the same answer structurally — a <see cref="PhaseReviewReport"/> OBJECT, not
/// a bare array, so a review that was skipped or failed says so instead of reading as a review
/// that found nothing — upserted as artifact <c>phase_review:&lt;phase id&gt;</c> beside the
/// record, which is what 2026-09-17-042ej reads.
/// </para>
/// </summary>
public sealed class PhaseReviewPublisher(IEventPublisher eventPublisher)
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public Task PublishAsync(
        PipelineContext pipeline, string phaseId, PhaseReviewReport report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(report);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new PhaseReviewedEvent(
                runId, phaseId, JsonSerializer.Serialize(report, Options), DateTimeOffset.UtcNow),
            ct);
    }
}
