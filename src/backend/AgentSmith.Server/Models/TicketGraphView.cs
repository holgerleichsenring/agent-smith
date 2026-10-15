namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcen: the tickets of a project that require or are required, and the requires between them.</summary>
public sealed record TicketGraphView(IReadOnlyList<TicketGraphNodeView> Nodes, IReadOnlyList<TicketGraphEdgeView> Edges);
