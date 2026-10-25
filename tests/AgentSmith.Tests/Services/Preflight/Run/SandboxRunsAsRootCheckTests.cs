using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>2026-10-25-c5a7a: a root project's runs carry a warning naming the project.</summary>
public sealed class SandboxRunsAsRootCheckTests
{
    [Fact]
    public async Task SandboxRunsAsRootCheck_RootProject_WarnsNamingIt()
    {
        var finding = await Check(hardening: true).RunAsync(Pipeline(runAsRoot: true), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn);
        finding.Message.Should().Contain("billing").And.Contain("sandbox.run_as_root");
    }

    [Fact]
    public async Task SandboxRunsAsRootCheck_HardenedProject_Passes()
    {
        var finding = await Check(hardening: true).RunAsync(Pipeline(runAsRoot: null), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    // 2026-10-25-ff3b: with hardening off every sandbox is root, and nobody chose it per project.
    [Fact]
    public async Task SandboxRunsAsRootCheck_HardeningOff_Passes()
    {
        var finding = await Check(hardening: false).RunAsync(Pipeline(runAsRoot: true), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    private static SandboxRunsAsRootCheck Check(bool hardening) =>
        new(Microsoft.Extensions.Options.Options.Create(new SandboxGlobalConfig { HardeningEnabled = hardening }));

    private static PipelineContext Pipeline(bool? runAsRoot)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "billing", Sandbox = new SandboxConfig { RunAsRoot = runAsRoot },
        });
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["api"] = new ScriptedSandbox() });
        return pipeline;
    }
}
