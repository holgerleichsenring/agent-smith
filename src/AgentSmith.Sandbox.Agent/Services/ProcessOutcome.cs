namespace AgentSmith.Sandbox.Agent.Services;

internal readonly record struct ProcessOutcome(int ExitCode, bool TimedOut, string? ErrorMessage);
