namespace AgentSmith.Application.Services.Sandbox;

/// <summary>2026-11-09-9631a: the sandbox a step was running on is gone, confirmed by its heartbeat
/// and its own exit read. Not a failure of the step: the caller may open a fresh sandbox.</summary>
public sealed class SandboxVanishedException(string jobId)
    : Exception($"Sandbox {jobId} is gone.");
