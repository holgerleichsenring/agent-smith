namespace AgentSmith.Server.Models;

/// <summary>2026-11-17-dd05c: the spec's whole yaml as the person edited it; its <c>spec:</c> field keeps the spec's id.</summary>
public sealed record TicketSpecEditRequest(string Yaml);
