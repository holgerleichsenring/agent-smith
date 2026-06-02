namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: which ISandboxFactory the harness wires. Stub is the default for
/// fast-tier handler-flow tests (no docker, no network). Docker swaps in
/// the production DockerSandboxFactory + a real IConnectionMultiplexer so
/// the harness exercises the full container spawn + git + dotnet path.
/// </summary>
public enum SandboxBackend
{
    Stub,
    Docker,
}
