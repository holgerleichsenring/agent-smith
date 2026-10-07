using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>2026-10-06-03c7e: where a record is committed — the carrying repository's sandbox
/// (and its key, null in a single-sandbox run), the ticket branch and the project.</summary>
public sealed record SeriesRecordTarget(ISandbox Sandbox, string? SandboxKey, string Branch, string Project);
