using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-10-13-8762v: what the worker does once the attempt is not live — the act to serve, or how the nudge ends.</summary>
public sealed record ReworkTail(ReworkNudgeDisposition Disposition, PendingReworkAct? Act = null);
