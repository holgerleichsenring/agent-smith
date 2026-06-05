using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

// p0230: sandbox timeouts are configurable in agentsmith.yml — a global default
// under `sandbox:` and a per-project override under `projects.<n>.sandbox:`.
// Resolution is project-override ?? global. Defaults apply when neither is set.
public sealed class SandboxTimeoutResolutionTests
{
    [Fact]
    public void ResolveTimeouts_NoProjectOverride_UsesGlobal()
    {
        var global = new SandboxGlobalConfig { StepTimeoutSeconds = 1200, RunCommandTimeoutSeconds = 420 };

        global.ResolveStepTimeout(null).Should().Be(1200);
        global.ResolveRunCommandTimeout(null).Should().Be(420);
        global.ResolveStepTimeout(new SandboxConfig()).Should().Be(1200);
        global.ResolveRunCommandTimeout(new SandboxConfig()).Should().Be(420);
    }

    [Fact]
    public void ResolveTimeouts_ProjectOverride_WinsOverGlobal()
    {
        var global = new SandboxGlobalConfig { StepTimeoutSeconds = 900, RunCommandTimeoutSeconds = 300 };
        var project = new SandboxConfig { StepTimeoutSeconds = 1800, RunCommandTimeoutSeconds = 600 };

        global.ResolveStepTimeout(project).Should().Be(1800);
        global.ResolveRunCommandTimeout(project).Should().Be(600);
    }

    [Fact]
    public void Defaults_AreSet_WhenNothingConfigured()
    {
        var global = new SandboxGlobalConfig();
        global.StepTimeoutSeconds.Should().Be(900);
        global.RunCommandTimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void Build_CarriesResolvedStepTimeout_OnSpec_FromProjectOverride()
    {
        var global = Options.Create(new SandboxGlobalConfig { StepTimeoutSeconds = 900 });
        var sut = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(), new StubAgentImageResolver(), global);
        var project = new ResolvedProject { Sandbox = new SandboxConfig { StepTimeoutSeconds = 1500 } };

        var spec = sut.Build(project, language: "csharp");

        spec.StepTimeoutSeconds.Should().Be(1500);
    }

    [Fact]
    public void Build_CarriesGlobalStepTimeout_WhenNoProjectOverride()
    {
        var global = Options.Create(new SandboxGlobalConfig { StepTimeoutSeconds = 777 });
        var sut = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(), new StubAgentImageResolver(), global);

        var spec = sut.Build(new ResolvedProject(), language: "csharp");

        spec.StepTimeoutSeconds.Should().Be(777);
    }
}
