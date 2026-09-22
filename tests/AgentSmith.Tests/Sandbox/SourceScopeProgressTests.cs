using System.Collections.Concurrent;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-17-c7aec: a source scope tells the ambient observer when it begins opening its
/// repository and how that ended — once each, from the one materialising method both the
/// lazy read and the eager open pass through — and tells nobody when none is set.
/// </summary>
public sealed class SourceScopeProgressTests
{
    private static readonly ResolvedProject Project = new() { Name = "p" };
    private readonly AsyncLocalSourceScopeObserverAccessor _observers = new();
    private readonly RecordingObserver _observer = new();

    [Fact]
    public async Task Scope_OnMaterialise_ReportsOpeningThenReady()
    {
        var sut = Scope(new StubSandboxFactory());
        using var observing = _observers.Observe(_observer);

        await sut.RunStepAsync(ReadStep(), null, CancellationToken.None);

        _observer.Reports.Should().Equal(
            ("repo-a", SourceScopeProgress.Opening), ("repo-a", SourceScopeProgress.Ready));
    }

    [Fact]
    public async Task Scope_WhenPreparationFails_ReportsFailed()
    {
        var factory = new FailingCloneFactory();
        var sut = Scope(factory);
        using var observing = _observers.Observe(_observer);

        var result = await sut.RunStepAsync(ReadStep(), null, CancellationToken.None);

        result.ExitCode.Should().NotBe(0);
        sut.IsMaterialized.Should().BeFalse();
        _observer.Reports.Should().Equal(
            ("repo-a", SourceScopeProgress.Opening), ("repo-a", SourceScopeProgress.Failed));
        factory.Spawned!.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Scope_AtARevision_ReportsTheRevisionWithTheName()
    {
        var sut = Scope(new StubSandboxFactory(), revision: "v1.2");
        using var observing = _observers.Observe(_observer);

        await sut.RunStepAsync(ReadStep(), null, CancellationToken.None);

        _observer.Reports.Select(r => r.Item1).Should().AllBe("repo-a@v1.2",
            "two templates may pin one repository at two revisions, and each is its own line");
    }

    [Fact]
    public async Task Scope_OpenedTwice_ReportsOnce()
    {
        var factory = new HeldSpawnFactory();
        var sut = Scope(factory);
        using var observing = _observers.Observe(_observer);

        // The read holds the gate while its spawn is held; the eager open is already waiting
        // on the gate behind it, past the first emptiness check.
        var read = sut.RunStepAsync(ReadStep(), null, CancellationToken.None);
        var eager = sut.MaterializeAsync(CancellationToken.None);
        factory.Release();
        await Task.WhenAll(read, eager);
        await sut.RunStepAsync(ReadStep(), null, CancellationToken.None);

        _observer.Reports.Should().Equal(
            ("repo-a", SourceScopeProgress.Opening), ("repo-a", SourceScopeProgress.Ready));
        factory.Inner.Spawned.Should().ContainSingle();
    }

    [Fact]
    public async Task Scope_WithNoObserverSet_BehavesAsBefore()
    {
        var factory = new StubSandboxFactory();
        var sut = Scope(factory);

        var sha = await sut.MaterializeAsync(CancellationToken.None);

        sha.Should().Be("stub-head");
        factory.Spawned.Should().ContainSingle();
        _observers.Current.Should().BeNull("no caller set one");
        _observer.Reports.Should().BeEmpty();
    }

    [Fact]
    public void Observe_Disposed_RestoresTheEnclosingObserver()
    {
        var outer = new RecordingObserver();

        using (_observers.Observe(outer))
        {
            using (_observers.Observe(_observer))
                _observers.Current.Should().BeSameAs(_observer);
            _observers.Current.Should().BeSameAs(outer);
        }

        _observers.Current.Should().BeNull();
    }

    private SourceScopeSandbox Scope(ISandboxFactory factory, string? revision = null)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        var opener = new SourceScopeOpener(
            new SourceScopeMaterialiser(), factory, specBuilder, Mock.Of<IRunContextAccessor>());
        var repo = new RepoConnection
        {
            Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a",
        };
        return new SourceScopeSandbox(
            Project, repo, revision, conversationId: null, opener, _observers,
            NullLogger<SourceScopeSandbox>.Instance);
    }

    private static Step ReadStep() => new(
        Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
        Command: null, Path: "/work/file.cs", Pattern: null, Content: null);

    private sealed class RecordingObserver : ISourceScopeObserver
    {
        private readonly ConcurrentQueue<(string, SourceScopeProgress)> _reports = new();
        public IReadOnlyList<(string, SourceScopeProgress)> Reports => [.. _reports];

        public Task ReportAsync(string repoName, SourceScopeProgress progress, CancellationToken ct)
        {
            _reports.Enqueue((repoName, progress));
            return Task.CompletedTask;
        }
    }

    /// <summary>Holds every spawn until released, so two openers meet at the gate.</summary>
    private sealed class HeldSpawnFactory : ISandboxFactory
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StubSandboxFactory Inner { get; } = new();

        public void Release() => _release.SetResult();

        public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
        {
            await _release.Task;
            return await Inner.CreateAsync(spec, cancellationToken);
        }
    }

    /// <summary>Spawns a sandbox whose clone the host refuses.</summary>
    private sealed class FailingCloneFactory : ISandboxFactory
    {
        public Mock<ISandbox>? Spawned { get; private set; }

        public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
        {
            var sandbox = Spawned = new Mock<ISandbox>();
            sandbox.Setup(s => s.RunStepAsync(
                    It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Step step, IProgress<StepEvent>? _, CancellationToken _) => new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 128, TimedOut: false,
                    DurationSeconds: 0, ErrorMessage: "fatal: unable to access", OutputContent: null));
            return Task.FromResult(sandbox.Object);
        }
    }
}
