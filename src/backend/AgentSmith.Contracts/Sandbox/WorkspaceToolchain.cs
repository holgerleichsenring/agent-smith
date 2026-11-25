namespace AgentSmith.Contracts.Sandbox;

/// <summary>2026-11-25-9992b: one standard toolchain image of a run's workspace and the limits of its container.</summary>
public sealed record WorkspaceToolchain(string Image, ResourceLimits Resources);
