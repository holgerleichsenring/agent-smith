namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-10-13-9a59l: what a never-worked ticket's approval told the thread, and whether the
/// approved series was recorded — an approval that recorded nothing keeps its proposal open.</summary>
public sealed record TicketAmendmentResult(string Notice, bool Recorded);
