using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Registry;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>2026-11-25-9992b: one container serves every repository of the workspace and shares one home, so it stages
/// the feeds of each — every repository root listed on its own.</summary>
public sealed class RegistryAuthWorkspaceRootsTests
{
    private static SetupRegistryAuthHandler Handler() => new(
        Mock.Of<ISandboxFileReaderFactory>(), new AgentSmithConfig(), null!, NullLogger<SetupRegistryAuthHandler>.Instance);

    [Fact]
    public void RootsOf_RunWithTwoRepositories_NamesEachRootOnce()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["web-node"] = "web", ["web-dotnet"] = "web", ["api"] = "api" });

        Handler().RootsOf(pipeline, new StepRecordingSandbox()).Should().Equal("/work/api", "/work/web");
    }

    [Fact]
    public void RootsOf_OutsideARun_IsTheSandboxsOwnRoot() =>
        Handler().RootsOf(new PipelineContext(), new StepRecordingSandbox()).Should().Equal("/work");
}
