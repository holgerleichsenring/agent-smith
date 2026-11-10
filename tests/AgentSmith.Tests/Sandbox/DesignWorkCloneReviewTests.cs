using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-10-4778 review: the work clone spawns like a coding run (root-first image, build sizing, the project's
/// secrets), keeps the credential helper a git option rather than a clone option, skips LFS, re-clones in full when
/// full history is asked after a shallow clone, reports its progress, leaves the held read scope where evidence reads
/// it, and gives back every resource even when one of them throws.
/// </summary>
public sealed class DesignWorkCloneReviewTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly ResourceLimits BuildSizing = new("2", "4", "4Gi", "8Gi");
    private static readonly RepoConnection Repo = new() { Name = "api", Url = "https://github.com/o/api", Type = RepoType.GitHub };
    private static readonly ResolvedProject Project = new()
    {
        Name = "sample", Repos = [Repo],
        Sandbox = new SandboxConfig { Secrets = new SandboxSecrets { Env = new() { ["SF_ID"] = "sf-creds:id" } } },
    };

    private sealed class Recording : ISandbox
    {
        public List<Step> Steps { get; } = [];
        public bool Disposed { get; private set; }
        public Exception? ThrowOnDispose { get; init; }
        public string JobId => "job-1";
        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Steps.Add(step);
            return Task.FromResult(new StepResult(Step.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, "out"));
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ThrowOnDispose is null ? ValueTask.CompletedTask : ValueTask.FromException(ThrowOnDispose);
        }
    }

    private sealed class Factory : ISandboxFactory
    {
        public List<(SandboxSpec Spec, Recording Sandbox)> Spawned { get; } = [];
        public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken ct)
        {
            var sandbox = new Recording();
            Spawned.Add((spec, sandbox));
            return Task.FromResult<ISandbox>(sandbox);
        }
    }

    private sealed class SizedByPipeline : ISandboxResourceResolver
    {
        public ResourceLimits Resolve(ResolvedProject projectConfig, string? pipelineName, ContextYamlStackResources? contextResources = null) =>
            pipelineName == PipelinePresets.CodeName ? BuildSizing : ResourceLimits.LightProfile;
        public SandboxResourceLayer ResolveLayer(ResolvedProject projectConfig, string? pipelineName, ContextYamlStackResources? contextResources = null) =>
            SandboxResourceLayer.GlobalDefault;
    }

    private sealed class Observer : ISourceScopeObserver
    {
        public List<(string Name, SourceScopeProgress Progress)> Told { get; } = [];
        public Task ReportAsync(string repoName, SourceScopeProgress progress, CancellationToken cancellationToken)
        {
            Told.Add((repoName, progress));
            return Task.CompletedTask;
        }
    }

    private static WorkCloneOpener Opener(Factory factory, ISourceScopeObserverAccessor? observers = null)
    {
        var languages = new Mock<ISandboxLanguageResolver>();
        languages.Setup(l => l.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RemoteContextDiscovery("web", "web", "node"), new RemoteContextDiscovery("api", ".", "csharp", ToolchainImage: "dotnet:9")]);
        var specs = new SandboxSpecBuilder(new SizedByPipeline(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        return new WorkCloneOpener(factory, specs, languages.Object, TestGitCredentials.Resolver,
            Mock.Of<IRunContextAccessor>(r => r.CurrentRunId == "run-9"), observers);
    }

    private static IReadOnlyDictionary<string, ISandbox> Held(out Mock<ISourceScopeSandbox> scope)
    {
        scope = new Mock<ISourceScopeSandbox>();
        scope.Setup(s => s.MaterializeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Sha);
        return new Dictionary<string, ISandbox> { ["api"] = scope.Object };
    }

    [Fact]
    public async Task WorkClone_Spawn_UsesResolvedImageBuildSizingProjectSecrets()
    {
        var spec = await Opener(new Factory()).SpecAsync(Project, Repo, default);

        spec.ToolchainImage.Should().Be("dotnet:9", "the root-first context's image");
        spec.Resources.Should().Be(BuildSizing, "a work clone builds, so it gets the code preset's sizing");
        spec.Secrets!.Env.Select(e => e.EnvName).Should().Equal("SF_ID");
    }

    [Fact]
    public void WorkClone_CloneStep_HelperIsAGitOptionBeforeClone_LfsSkipped()
    {
        var args = WorkCloneOpener.CloneStep(Repo, TestGitCredentials.Resolver.For(Repo), fullHistory: false).Args!.ToList();
        var helper = args.FindIndex(a => a.StartsWith("credential.helper=", StringComparison.Ordinal));

        helper.Should().BeGreaterThan(0);
        args[helper - 1].Should().Be("-c");
        helper.Should().BeLessThan(args.IndexOf("clone"), "after clone, -c would be written into the clone's .git/config");
        WorkCloneOpener.CloneStep(Repo, TestGitCredentials.Resolver.For(Repo), false).Env.Should()
            .Contain("GIT_LFS_SKIP_SMUDGE", "1").And.ContainKey("GIT_TOKEN");
    }

    [Fact]
    public async Task WorkClone_HistoryFullOnAShallowClone_DisposesItAndClonesFresh()
    {
        var factory = new Factory();
        await using var clones = new WorkCloneSet(Opener(factory), Project, [Repo]);
        var held = Held(out _);

        var shallow = await clones.ForAsync("api", fullHistory: false, held, default);
        var full = await clones.ForAsync("api", fullHistory: true, held, default);
        var again = await clones.ForAsync("api", fullHistory: true, held, default);

        factory.Spawned.Should().HaveCount(2);
        ((Recording)shallow).Disposed.Should().BeTrue("a shallow clone is never deepened in place");
        full.Should().NotBeSameAs(shallow).And.BeSameAs(again);
        ((Recording)full).Steps[0].Args.Should().NotContain("--depth");
    }

    [Fact]
    public async Task WorkClone_Open_ReportsOpeningThenReadyThroughTheScopeObserver()
    {
        var observers = new AsyncLocalSourceScopeObserverAccessor();
        var observer = new Observer();
        using var _ = observers.Observe(observer);

        await Opener(new Factory(), observers).OpenAsync(Project, Repo, Sha, fullHistory: false, default);

        observer.Told.Should().Equal(
            (WorkCloneOpener.ProgressName("api"), SourceScopeProgress.Opening),
            (WorkCloneOpener.ProgressName("api"), SourceScopeProgress.Ready));
    }

    [Fact]
    public async Task ProposalEvidence_AfterMasterRun_ReadsTheHeldScope()
    {
        var held = Held(out var scope);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.WorkClones, new WorkCloneSet(Opener(new Factory()), Project, [Repo]));
        pipeline.Set(ContextKeys.Sandboxes, held);
        pipeline.Set(ContextKeys.MayStartRuns, true);
        var run = new DesignWorkToolGate(new SandboxContainerRuntime(true), Mock.Of<ISandboxBusIsolation>(b => !b.IsShared))
            .Tools(pipeline, new MasterRecordTools(null!, null!, null!, null, null!, null))
            .OfType<AIFunction>().Single(t => t.Name == "run_command");

        await run.InvokeAsync(new AIFunctionArguments { ["command"] = "echo x > README.md", ["repo"] = "api" });

        var evidence = pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        evidence["api"].Should().BeSameAs(scope.Object, "evidence checks and reviewers read the held scope, never the clone");
        evidence.Should().HaveCount(1);
    }

    [Fact]
    public async Task DesignTurnResources_OneDisposeThrows_ClonesFirstAndEveryOtherStillGivenBack()
    {
        var order = new List<string>();
        var factory = new Factory();
        string Clone() => factory.Spawned.Single().Sandbox.Disposed ? "after the clone" : "before the clone";
        var failing = new Mock<ISandbox>();
        failing.Setup(s => s.DisposeAsync()).Callback(() => order.Add($"a {Clone()}")).Returns(ValueTask.FromException(new IOException("gone")));
        var fine = new Mock<ISandbox>();
        fine.Setup(s => s.DisposeAsync()).Callback(() => order.Add($"b {Clone()}")).Returns(ValueTask.CompletedTask);
        var clones = new WorkCloneSet(Opener(factory), Project, [Repo]);
        await clones.ForAsync("api", false, Held(out _), default);
        var resources = new DesignTurnResources(new Dictionary<string, ISandbox> { ["a"] = failing.Object, ["b"] = fine.Object }, clones);

        var dispose = async () => await resources.DisposeAsync();

        await dispose.Should().ThrowAsync<AggregateException>();
        factory.Spawned.Single().Sandbox.Disposed.Should().BeTrue();
        order.Should().Equal("a after the clone", "b after the clone");
    }
}
