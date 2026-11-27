using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-f5f7e fast-tier end-to-end: a series over Server and Client, over a scope that also holds a
/// worker. 2026-11-27-ecf2c: over several repositories derivation opens no draft spec pull request — which will
/// carry code is not known yet — and the run dies before any spec gained code, so every branch carries only the
/// spec record and no repository gets a pull request.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesSpecPullRequestsTests
{
    [Fact]
    public async Task SeriesOverServerAndClient_RunEndsWithoutCode_OpensNoPullRequest()
    {
        var pullRequests = new PerRepoPullRequestProvider();
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(new RecordingTicketProvider()));
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(pullRequests);
        });
        harness.ChatClient
            .EnqueueScopeReply("no verdict")
            .EnqueueThrow(new MasterBudgetExhaustedException("per-pipeline cost budget exhausted: 15.02 USD of 15.00 USD spent"));

        var runner = new PipelineRunner(harness.Services) { ReposOverride = Repos(), DoneStatus = "done", FailedStatus = "failed" }
            .WithSeries([new HarnessSeries.Member("Introduce the empty-payload guard", "An empty request body is answered with 400.", "sample-server"),
            new HarnessSeries.Member("Move the existing callers onto the guard", "No caller builds its own empty-payload check.", "sample-client")]);
        await runner.RunAsync("code");

        var pipeline = runner.LastContext!;
        SpecPullRequests.Of(pipeline).Should().BeEmpty();
        pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests)
            .Should().NotContain(o => o.Status == OpenStatus.Opened, "a branch with only the spec record gets no pull request");
        pullRequests.Bodies.Should().BeEmpty();
    }

    private static IReadOnlyList<RepoConnection> Repos() =>
    [
        new() { Name = "sample-server", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-server" },
        new() { Name = "sample-client", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-client" },
        new() { Name = "sample-worker", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-worker" },
    ];
}
