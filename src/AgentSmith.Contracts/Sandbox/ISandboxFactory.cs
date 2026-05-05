namespace AgentSmith.Contracts.Sandbox;

public interface ISandboxFactory
{
    Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken);
}
