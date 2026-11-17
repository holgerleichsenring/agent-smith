using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-17-9a39a: what keeps a ticket from its start. <see cref="Unmet"/> holds it; <see cref="StackedOn"/> is
/// the prerequisite whose pushed branch it starts on, null when it starts on its own base. <see cref="GreenLifts"/> says
/// a green pushed branch would have lifted a requirement — the project stacks and the green read is composed.
/// </summary>
public sealed record RequirementHold(
    bool HasRequirements, IReadOnlyList<TicketRequirementEdge> Unmet, TicketRequirementEdge? StackedOn = null,
    bool GreenLifts = false);
