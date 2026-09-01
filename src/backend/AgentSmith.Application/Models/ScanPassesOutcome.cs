using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-01-7df4: what a scan master's follow-up passes leave behind — the loop result
/// whose duration and usage describe the pass that ran last, and the ANSWER the run
/// publishes, which is the union of every pass rather than the last one's alone.
/// </summary>
public sealed record ScanPassesOutcome(AgenticLoopResult Result, string Answer);
