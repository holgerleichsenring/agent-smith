using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0201: default binding for compositions without a Docker backend (CLI
/// dev-loop with InProcessSandbox, unit-test fixtures). Server composition
/// overrides this with <c>SandboxLivenessSupervisor</c>.
/// </summary>
public sealed class NoOpSandboxLivenessSupervisor : ISandboxLivenessSupervisor
{
    public void Watch(string runId, string sandboxKey, ISandbox sandbox) { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
