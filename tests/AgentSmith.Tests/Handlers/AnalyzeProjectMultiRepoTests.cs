using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
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

// p0384: the "primary repo" collapse (sandboxes.Keys.First()) is dead — the
// per-repo dictionaries are the ONLY analysis surface, so a 3-repo run's plan,
// contract and master sections see all 3 repos (ticket #19106 root cause).
public sealed class AnalyzeProjectMultiRepoTests
{
    [Fact]
    public async Task AnalyzeProject_MultiRepo_PublishesPerRepoMaps_NoSingularCollapse()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["server"] = GitSandbox(),
                ["client"] = GitSandbox(),
                ["docs"] = GitSandbox(),
            });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["server"] = new("default", ".", "csharp"),
                ["client"] = new("default", ".", "typescript"),
                ["docs"] = new("default", ".", "markdown"),
            });

        var mapStore = new Mock<IProjectMapStore>();
        mapStore.Setup(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((key, _, _) => Task.FromResult<ProjectMap?>(MapFor(key)));

        var handler = new AnalyzeProjectHandler(
            Mock.Of<IProjectAnalyzer>(),
            new StubSandboxFileReaderFactory(),
            mapStore.Object,
            new SandboxGitOperations(
                NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            Mock.Of<IRunArtifactStore>(),
            new ProjectMapCacheKey(), new SandboxTargets(), NullLogger<AnalyzeProjectHandler>.Instance);

        var result = await handler.ExecuteAsync(
            new AnalyzeCodeContext(new Repository(new BranchName("main"), "git://x"), pipeline),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var maps = pipeline.Get<IReadOnlyDictionary<string, ProjectMap>>(ContextKeys.RepoProjectMaps);
        maps.Keys.Should().BeEquivalentTo("server", "client", "docs");
        maps["client"].PrimaryLanguage.Should().Be("client-lang");
        var codeMaps = pipeline.Get<IReadOnlyDictionary<string, string>>(ContextKeys.RepoCodeMaps);
        codeMaps.Keys.Should().BeEquivalentTo("server", "client", "docs");
        codeMaps["server"].Should().Contain("server-lang");
        // The retired singular slots must never reappear — no consumer may fall
        // back to an arbitrary "primary" repo.
        pipeline.Has("ProjectMap").Should().BeFalse();
        pipeline.Has("CodeMap").Should().BeFalse();
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
