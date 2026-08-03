using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a security: derivation is the trust boundary. An embedded instruction that
/// is not a requirement has NO SLOT in the schema, so it cannot quietly become
/// scope; it is reported through p0316's existing refusal record instead.
/// Structural narrowing, not vigilance — the deriver still reads untrusted text and
/// is influenceable, but it has no tools, no repo write and a schema-bounded output.
/// </summary>
public sealed class SpecRefusalReporter(
    IEventPublisher events, ILogger<SpecRefusalReporter> logger)
{
    public async Task ReportAsync(
        PipelineContext pipeline, IReadOnlyList<IgnoredInstruction> ignored, CancellationToken ct)
    {
        if (ignored is not { Count: > 0 }) return;
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        foreach (var instruction in ignored)
        {
            try
            {
                await events.PublishAsync(new TicketInstructionIgnoredEvent(
                    runId!, instruction.Quote, instruction.Reason, DateTimeOffset.UtcNow), ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to publish TicketInstructionIgnored event from derivation");
            }
        }
        logger.LogInformation(
            "Spec derivation refused {Count} ticket-embedded instruction(s)", ignored.Count);
    }
}
