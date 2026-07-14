using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>p0324: sandbox-spawn reports the composed backend's probe verdict.</summary>
public sealed class SandboxSpawnCheckTests
{
    [Fact]
    public async Task RunAsync_ProbeOk_PassesWithBackendLabel()
    {
        var check = new SandboxSpawnCheck(
            new ScriptedSandboxProbe("Kubernetes", ConnectionProbeResult.Reachable(45)));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("Kubernetes");
    }

    [Fact]
    public async Task RunAsync_ProbeFails_FailsActionable()
    {
        var check = new SandboxSpawnCheck(new ScriptedSandboxProbe(
            "Docker", ConnectionProbeResult.Unreachable(5000, "docker daemon not running")));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("docker daemon not running");
        result.FixHint.Should().Contain("runtime");
    }

    private sealed class ScriptedSandboxProbe(string label, ConnectionProbeResult result) : IPreflightSandboxProbe
    {
        public string BackendLabel => label;

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
