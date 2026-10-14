namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcen: what one ticket waits for and what waits for it.</summary>
public sealed record TicketRequiresView(IReadOnlyList<TicketRequirementView> Requires, IReadOnlyList<TicketRequirementView> RequiredBy);
