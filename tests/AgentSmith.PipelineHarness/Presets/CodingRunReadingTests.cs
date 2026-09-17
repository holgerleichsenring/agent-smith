using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-17-c7aec: the source scope is shared, so the progress a design turn hears must not
/// leak into the runs that open the same scopes. A coding run with a template sets no
/// observer, and the scopes it opens tell nobody.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CodingRunReadingTests
{
    [Fact]
    public async Task CodingRun_WithTemplates_ReportsNothing()
    {
        var observers = new WatchedAccessor();
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), services =>
            {
                HarnessProjectAnalyzerStub.Register(services);
                services.RemoveAll<ISourceScopeObserverAccessor>();
                services.AddSingleton<ISourceScopeObserverAccessor>(observers);
            });
        harness.ChatClient.EnqueueText(SpecDerivationFixture.DerivationJson);

        await new PipelineRunner(harness.Services)
        {
            TemplatesOverride = [new ProjectTemplate("default", "default", "v1.0.0", Reference())],
        }.RunAsync("code");

        harness.StubSandboxFactory!.Spawned.Should().Contain(
            s => s.Sandbox.RanSteps.Any(step => step.Args != null && step.Args.Contains(Reference().Url)),
            "the run did open the template, so its scope did ask for an observer");
        observers.Asked.Should().BeGreaterThan(0);
        observers.Answered.Should().Be(0, "a run that sets no observer is told nothing");
    }

    private static RepoConnection Reference() => new()
    {
        Name = "reference", Type = RepoType.Local,
        Path = "/tmp", Url = "https://stub.test/reference",
    };

    /// <summary>The real accessor, counting how often a scope asked and found an observer.</summary>
    private sealed class WatchedAccessor : ISourceScopeObserverAccessor
    {
        private readonly AsyncLocalSourceScopeObserverAccessor _inner = new();
        private int _asked;
        private int _answered;

        public int Asked => _asked;
        public int Answered => _answered;

        public ISourceScopeObserver? Current
        {
            get
            {
                Interlocked.Increment(ref _asked);
                var current = _inner.Current;
                if (current is not null) Interlocked.Increment(ref _answered);
                return current;
            }
        }

        public IDisposable Observe(ISourceScopeObserver observer) => _inner.Observe(observer);
    }
}
