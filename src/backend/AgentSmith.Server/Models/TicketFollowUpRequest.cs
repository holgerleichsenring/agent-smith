namespace AgentSmith.Server.Models;

/// <summary>2026-11-18-c8e2d: which proposed follow-up to file — the run and spec whose master proposed it, and its
/// place in that spec's report. The text is read on the server from the stored report.</summary>
public sealed record TicketFollowUpRequest(string RunId, string SpecId, int Index);
