using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933dc: while PoCs are on, a server whose sandboxes run in process (no image) is named, because
/// every PoC upload's dependency scan needs a backend that runs the pinned scanner image.</summary>
public sealed class PocSandboxFindingTests
{
    [Fact]
    public void Report_PocsOnInProcess_IsNamed_OnAContainerBackendOrOff_ItIsNot()
    {
        var findings = new StartupFindings();
        var on = new AgentSmithConfig { Pocs = new PocsConfig { Enabled = true } };

        new PocSandboxFinding(new SandboxContainerRuntime(false), findings).Report(on);
        findings.All.Should().ContainSingle(f => f.Subsystem == PocSandboxFinding.Subsystem).Which.Reason.Should().Contain("Kubernetes or Docker");

        new PocSandboxFinding(new SandboxContainerRuntime(true), findings).Report(on);
        new PocSandboxFinding(new SandboxContainerRuntime(false), findings).Report(new AgentSmithConfig());
        findings.All.Should().NotContain(f => f.Subsystem == PocSandboxFinding.Subsystem);
    }
}
