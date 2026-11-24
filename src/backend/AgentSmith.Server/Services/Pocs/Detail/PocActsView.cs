namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>2026-11-23-c81ed: which acts the detail page offers — PocActPlan's table for the PoC's state, all false for a
/// caller who may not act on it.</summary>
internal sealed record PocActsView(bool Stop, bool Start, bool Rollback, bool Apply, bool Delete);
