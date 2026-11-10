using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-10-4778: a design turn's work clone — its only credentialed step is the clone, before every model step;
/// it lands on the commit the reads show or fails closed; its spec carries the run and no conversation, no package
/// cache and a per-job bus; the turn's resources dispose it; and run_command is offered only to a poster with
/// runs.control, on containers, on a per-job bus.
/// </summary>
public sealed class DesignWorkCloneTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly RepoConnection Repo = new() { Name = "api", Url = "https://github.com/o/api", Type = RepoType.GitHub };
    private static readonly ResolvedProject Project = new() { Name = "sample", Repos = [Repo] };

    private sealed class Recording(int checkoutExit = 0) : ISandbox
    {
        public List<Step> Steps { get; } = [];
        public bool Disposed { get; private set; }
        public string JobId => "job-1";
        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Steps.Add(step);
            var exit = step.Args?.Contains("checkout") == true ? checkoutExit : 0;
            return Task.FromResult(new StepResult(Step.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, "out"));
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Factory(Recording sandbox) : ISandboxFactory
    {
        public SandboxSpec? Spec { get; private set; }
        public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken ct)
        {
            Spec = spec;
            return Task.FromResult<ISandbox>(sandbox);
        }
    }

    private static WorkCloneOpener Opener(Factory factory, string? runId = "run-9")
    {
        var languages = new Mock<ISandboxLanguageResolver>();
        languages.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RemoteContextDiscovery("web", "web", "node"), new RemoteContextDiscovery("api", ".", "csharp", ToolchainImage: "dotnet:9")]);
        var specs = new SandboxSpecBuilder(new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        return new WorkCloneOpener(factory, specs, languages.Object, TestGitCredentials.Resolver,
            Mock.Of<IRunContextAccessor>(r => r.CurrentRunId == runId));
    }

    private static Mock<ISourceScopeSandbox> Held(string sha = Sha)
    {
        var scope = new Mock<ISourceScopeSandbox>();
        scope.Setup(s => s.MaterializeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(sha);
        return scope;
    }

    private static PipelineContext Turn(WorkCloneSet clones, bool mayStartRuns = true)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.WorkClones, clones);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["api"] = Held().Object });
        if (mayStartRuns) pipeline.Set(ContextKeys.MayStartRuns, true);
        return pipeline;
    }

    private static MasterRecordTools NoRecords() => new(null!, null!, null!, null, null!, null);

    private static ISandboxBusIsolation Bus(bool shared) => Mock.Of<ISandboxBusIsolation>(b => b.IsShared == shared);

    [Fact]
    public async Task WorkClone_RecordingSandbox_ExactlyOneStepCarriesTheToken_BeforeEveryModelStep()
    {
        var sandbox = new Recording();
        var clones = new WorkCloneSet(Opener(new Factory(sandbox)), Project, [Repo]);
        var tool = new DesignWorkToolGate(new SandboxContainerRuntime(true), Bus(shared: false))
            .Tools(Turn(clones), NoRecords()).OfType<AIFunction>().Single(t => t.Name == "run_command");

        await tool.InvokeAsync(new AIFunctionArguments { ["command"] = "git log -p", ["repo"] = "api" });
        await tool.InvokeAsync(new AIFunctionArguments { ["command"] = "dotnet test", ["repo"] = "api" });

        sandbox.Steps.Count(s => s.Env?.ContainsKey("GIT_TOKEN") == true).Should().Be(1);
        sandbox.Steps[0].Env!.Should().ContainKey("GIT_TOKEN");
        sandbox.Steps[0].Args.Should().Contain("clone");
        sandbox.Steps[1].Args.Should().Equal("checkout", "--detach", Sha);
        sandbox.Steps.Skip(1).Should().OnlyContain(s => s.Env == null || !s.Env.ContainsKey("GIT_TOKEN"));
        sandbox.Steps.Should().HaveCount(4, "one clone, one checkout, then the two commands in the same clone");
    }

    [Fact]
    public void WorkClone_DefaultDepth50_HistoryFullClonesWithoutDepth()
    {
        var credential = TestGitCredentials.Resolver.For(Repo);

        WorkCloneOpener.CloneStep(Repo, credential, fullHistory: false).Args.Should().ContainInOrder("--depth", "50", "--single-branch");
        WorkCloneOpener.CloneStep(Repo, credential, fullHistory: true).Args.Should().NotContain("--depth").And.Contain("--single-branch");
    }

    [Fact]
    public async Task WorkClone_ChecksOutTheHeldScopeSha_MissingShaFailsClosed()
    {
        var sandbox = new Recording(checkoutExit: 1);

        var open = () => Opener(new Factory(sandbox)).OpenAsync(Project, Repo, Sha, fullHistory: false, default);

        (await open.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{Sha}*history: full*");
        sandbox.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task WorkClone_Spec_CarriesRunIdNoConversationIdNoPackageCaches()
    {
        var spec = await Opener(new Factory(new Recording())).SpecAsync(Project, Repo, default);

        (spec.RunId, spec.ConversationId, spec.NoPackageCaches, spec.RequiresPerJobBus).Should().Be(("run-9", (string?)null, true, true));
        spec.ToolchainImage.Should().Be("dotnet:9", "the root-first context's image");
    }

    [Fact]
    public async Task DesignTurnResources_Dispose_DisposesScopesAndWorkClones()
    {
        var sandbox = new Recording();
        var scope = Held();
        var resources = new DesignTurnResources(new Dictionary<string, ISandbox> { ["api"] = scope.Object },
            new WorkCloneSet(Opener(new Factory(sandbox)), Project, [Repo]));
        await resources.Clones.ForAsync("api", false, new Dictionary<string, ISandbox> { ["api"] = scope.Object }, default);

        await resources.DisposeAsync();

        sandbox.Disposed.Should().BeTrue();
        scope.Verify(s => s.DisposeAsync(), Times.Once);
        resources.Clones.Opened.Should().Be(0, "the next turn clones fresh");
    }

    [Theory]
    [InlineData(false, false, true)] // in-process runtime
    [InlineData(true, true, true)] // shared bus credential
    [InlineData(true, false, false)] // poster without runs.control
    public void DesignWorkTools_WithoutContainersBusOrRight_Absent(bool containers, bool sharedBus, bool mayStartRuns)
    {
        var clones = new WorkCloneSet(Opener(new Factory(new Recording())), Project, [Repo]);
        var gate = new DesignWorkToolGate(new SandboxContainerRuntime(containers), Bus(sharedBus));

        gate.Tools(Turn(clones, mayStartRuns), NoRecords()).Select(t => t.Name).Should().NotContain("run_command");
    }

    [Fact]
    public void DesignWorkTools_DefaultBusIsolation_ReportsSharedNoTools()
    {
        var clones = new WorkCloneSet(Opener(new Factory(new Recording())), Project, [Repo]);

        new DesignWorkToolGate(new SandboxContainerRuntime(true)).MayRun(Turn(clones)).Should().BeFalse();
        new DesignWorkToolGate(new SandboxContainerRuntime(true), Bus(shared: false)).MayRun(Turn(clones)).Should().BeTrue();
    }

    [Fact]
    public async Task SandboxFactories_RequiresPerJobBus_SharedCredential_FailsClosed()
    {
        var shared = SandboxBusCredential.Shared("redis://server");
        await using var perJob = new SandboxBusCredential("redis://job", () => Task.CompletedTask);

        var refuse = () => shared.Require(perJob: true);

        refuse.Should().Throw<InvalidOperationException>().WithMessage("*shared one*");
        shared.Require(perJob: false).Should().BeSameAs(shared);
        perJob.Require(perJob: true).Should().BeSameAs(perJob);
    }

    [Fact]
    public void SpecDialogSurface_ChildHasNoRunCommand()
    {
        var run = AIFunctionFactory.Create(() => "x", name: "run_command");
        var read = AIFunctionFactory.Create(() => "x", name: "read_file");

        SpecDialogChildTools.Of([run, read]).Select(t => t.Name).Should().Equal("read_file");
    }
}
