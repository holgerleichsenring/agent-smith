namespace AgentSmith.Contracts.Sandbox;

public sealed record SandboxSecurityContext(long FsGroup = 1000, long? RunAsUser = null);
