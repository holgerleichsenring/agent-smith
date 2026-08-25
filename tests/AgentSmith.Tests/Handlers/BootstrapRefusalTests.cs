using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0496: the bootstrap refusal states what it read. Three runs in a row aborted with
/// "the repo may be empty or newly renamed" while the file the probe wanted was sitting on
/// the base branch — the operator was sent to check a repository name that was correct.
/// </summary>
public sealed class BootstrapRefusalTests
{
    private const string WorkBranch = "agent-smith/19106";
    private const string BaseBranch = "main";

    [Fact]
    public async Task BootstrapRefusal_NamesTheProbedBranch_AndItsBase()
    {
        var refusal = await RefuseAsync();

        refusal.Should().Contain(WorkBranch).And.Contain(BaseBranch);
    }

    [Fact]
    public async Task BootstrapRefusal_NamesTheProbedPaths()
    {
        var refusal = await RefuseAsync();

        refusal.Should()
            .Contain("/work/.agentsmith/contexts/default/context.yaml")
            .And.Contain("/work/.agentsmith/contexts/default/coding-principles.md");
    }

    [Fact]
    public async Task BootstrapRefusal_NoLongerClaimsTheRepoMayBeEmptyOrRenamed()
    {
        var refusal = await RefuseAsync();

        refusal.Should().NotContain("empty or newly renamed");
        refusal.Should().Contain("init-project", "the actionable half of the old text stays");
    }

    private static async Task<string> RefuseAsync()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName(WorkBranch), "https://x/server.git"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["server"] = OriginHeadSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["server"] = new RemoteContextDiscovery("default", ".", "csharp")
            });
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            PipelineName: "fix-bug", Agent: new AgentConfig(), SkillsPath: "skills",
            CodingPrinciplesPath: null));

        await CheckHandler().ExecuteAsync(new BootstrapCheckContext(pipeline), CancellationToken.None);
        var gate = await new BootstrapGateHandler(
                RunStateConceptsTestFactory.Default, EventTestStubs.NoOp,
                NullLogger<BootstrapGateHandler>.Instance)
            .ExecuteAsync(new BootstrapGateContext(pipeline), CancellationToken.None);

        gate.IsSuccess.Should().BeFalse();
        return gate.Message ?? string.Empty;
    }

    private static BootstrapCheckHandler CheckHandler()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var readerFactory = new Mock<ISandboxFileReaderFactory>();
        readerFactory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return new BootstrapCheckHandler(
            new BootstrapContextProbe(readerFactory.Object, NullLogger<BootstrapContextProbe>.Instance),
            TestGit.BaseBranch,
            RunStateConceptsTestFactory.Default,
            new SandboxTargets(), NullLogger<BootstrapCheckHandler>.Instance);
    }

    // The clone's own answer to "what do you merge into", which is where the missing file was.
    private static ISandbox OriginHeadSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                    $"origin/{BaseBranch}\n")));
        return sandbox.Object;
    }
}
