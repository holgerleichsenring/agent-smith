namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-25-0099b: GET /api/delivery — a period of ticket work in the caller's projects.
/// </summary>
/// <param name="Projects">The projects the caller may read, for the filter chips.</param>
/// <param name="Slots">The figures per slot of the period, oldest first.</param>
/// <param name="Truncated">The read hit its ceiling; the oldest attempts, the previous period's first, are missing.</param>
public sealed record DeliveryView(
    string Period,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<string> Projects,
    DeliveryFigures Current,
    DeliveryFigures Previous,
    IReadOnlyList<DeliveryFigures> Slots,
    IReadOnlyList<DeliveryEndCauseView> EndCauses,
    IReadOnlyList<DeliveryProjectView> ByProject,
    IReadOnlyList<DeliveryTicketView> Tickets,
    bool Truncated);
