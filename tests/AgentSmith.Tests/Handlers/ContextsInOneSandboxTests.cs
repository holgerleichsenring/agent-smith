using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-04-0721: two contexts of one repository that share a toolchain image share a
/// SANDBOX, and only the first of them is that sandbox's representative. Observed live on a
/// Node repository with a `backend` and a `frontend` context: the re-init reported success
/// having bootstrapped `backend` alone, so `frontend` kept the retired principles file and
/// gained no verify block — and the next coding run was refused by a gate that probes both.
/// </summary>
public sealed class ContextsInOneSandboxTests
{
    private const string SandboxKey = "default";
    private const string RepoName = "node-service";

    [Fact]
    public async Task ReInit_TwoContextsInOneSandbox_ProjectsBoth()
    {
        var pipeline = PipelineWithTwoContexts(
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", "typescript"));

        var components = await ProjectAsync(pipeline);

        components.Select(c => c.Name).Should().BeEquivalentTo(["backend", "frontend"]);
        components.Single(c => c.Name == "frontend").Workdir.Should().Be("frontend");
    }

    [Fact]
    public async Task ReInit_AProjectedContextWithoutALanguage_TakesItsSandboxs()
    {
        // A context declaring an image but no stack.lang was legal while it was never
        // projected. Projected with an empty slug it fails BootstrapDispatch and takes the
        // whole re-init with it — so the group's one toolchain answers for it.
        var pipeline = PipelineWithTwoContexts(
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", null));

        var components = await ProjectAsync(pipeline);

        components.Single(c => c.Name == "frontend").Language.Should().Be("typescript");
    }

    [Fact]
    public async Task Analyze_TwoContextsInOneSandbox_MapsEachContextsOwnSubtree()
    {
        var pipeline = PipelineWithTwoContexts(
            new RemoteContextDiscovery("backend", "backend", "typescript"),
            new RemoteContextDiscovery("frontend", "frontend", "typescript"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [SandboxKey] = GitSandbox() });

        var result = await NewAnalyzeHandler().ExecuteAsync(
            new AnalyzeCodeContext(new Repository(new BranchName("main"), "git://x"), pipeline),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var byContext = pipeline.Get<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ProjectMap>>>(
            ContextKeys.ContextProjectMaps)[RepoName];
        byContext.Keys.Should().BeEquivalentTo("backend", "frontend");
        // The analyzer is cached per (sandbox key, workdir), so a map naming the sibling's
        // workdir is a map of the wrong subtree — which is what a round would have written
        // that context.yaml from.
        byContext["frontend"].PrimaryLanguage.Should().Be($"{SandboxKey}@frontend-lang");
        byContext["backend"].PrimaryLanguage.Should().Be($"{SandboxKey}@backend-lang");
        pipeline.Get<IReadOnlyDictionary<string, ProjectMap>>(ContextKeys.RepoProjectMaps)[SandboxKey]
            .PrimaryLanguage.Should().Be($"{SandboxKey}@backend-lang", "the per-sandbox map stays the representative's");
    }

    private static async Task<IReadOnlyList<DiscoveredComponent>> ProjectAsync(PipelineContext pipeline)
    {
        var handler = new BootstrapDiscoverHandler(
            Mock.Of<IChatClientFactory>(), null, EventTestStubs.RunContext,
            new DiscoveryOutputParser(), new SandboxTargets(),
            new AgentSmith.Application.Services.Tools.AgenticToolSurface(),
            NullLogger<BootstrapDiscoverHandler>.Instance);
        var result = await handler.ExecuteAsync(
            new BootstrapDiscoverContext(RepoName, new AgentConfig(), pipeline), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("re-init", "the short-circuit must not call the model");
        return pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            ContextKeys.DiscoveredComponents)[RepoName];
    }

    private static PipelineContext PipelineWithTwoContexts(
        RemoteContextDiscovery representative, RemoteContextDiscovery sibling)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, new[] { new RepoConnection { Name = RepoName, Url = "https://x/y.git", Auth = "t" } });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal) { [SandboxKey] = representative });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [SandboxKey] = [representative, sibling],
            });
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos,
            new Dictionary<string, string>(StringComparer.Ordinal) { [SandboxKey] = RepoName });
        return pipeline;
    }

    private static AnalyzeProjectHandler NewAnalyzeHandler()
    {
        var mapStore = new Mock<IProjectMapStore>();
        mapStore.Setup(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((key, _, _) => Task.FromResult<ProjectMap?>(MapFor(key)));
        return new AnalyzeProjectHandler(
            Mock.Of<IProjectAnalyzer>(), new StubSandboxFileReaderFactory(), mapStore.Object,
            new SandboxGitOperations(
                new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
                new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            Mock.Of<IRunArtifactStore>(), new ProjectMapCacheKey(), new SandboxTargets(),
            NullLogger<AnalyzeProjectHandler>.Instance);
    }

    private static ISandbox GitSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, "sha-1234567")));
        return sandbox.Object;
    }

    private static ProjectMap MapFor(string cacheKey) => new(
        $"{cacheKey}-lang", [], [], [], [],
        new Conventions(null, null, null), new CiConfig(false, null, null, null));
}
