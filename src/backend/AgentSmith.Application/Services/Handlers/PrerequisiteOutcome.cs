namespace AgentSmith.Application.Services.Handlers;

/// <summary>p0202e: what one sandbox key's context.yaml prerequisite did; skipped when it declares none.</summary>
public sealed record PrerequisiteOutcome(string Key, int ExitCode, bool Skipped, string? Output = null);
