namespace AgentSmith.Contracts.Sandbox;

public sealed record ResourceLimits(string Memory = "2Gi", double CpuCores = 1.0);
