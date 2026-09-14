using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-a3f1: what the framework's own filing labels mean to routing, read in one
/// place rather than spelled out again at each decision.
/// <para>
/// A PHASE ticket is work and hard-binds to phase execution. An EPIC record is the summary
/// of a cut and is not work at all — it is refused before every other rule, because every
/// other rule ends in something and one of them would otherwise claim it.
/// </para>
/// </summary>
public static class FiledTicketLabels
{
    public static bool IsPhaseTicket(IncomingTicketEnvelope envelope) =>
        Carries(envelope, PhaseTicketRenderer.PhaseLabel);

    public static bool IsEpicRecord(IncomingTicketEnvelope envelope) =>
        Carries(envelope, PhaseTicketRenderer.EpicLabel);

    private static bool Carries(IncomingTicketEnvelope envelope, string label)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase));
    }
}
