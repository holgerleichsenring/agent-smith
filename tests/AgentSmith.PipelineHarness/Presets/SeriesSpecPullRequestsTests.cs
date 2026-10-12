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
/// worker. Each series repository gets its own draft spec pull request at derivation; the run then dies
/// before any phase gained code, so both are spec-only — and both are still listed as opened, carry the
/// run's body and link each other. The worker, which no spec names, gets none.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesSpecPullRequestsTests
{
    private const string ServerPr = "https://stub.test/sample-server/pull/1";
    private const string ClientPr = "https://stub.test/sample-client/pull/1";

    private static readonly string OneSpecEach = SpecDerivationFixture.TwoPhaseJson
        .Replace("\"slug\": \"introduce-the-guard\",", "\"slug\": \"introduce-the-guard\", \"repos\": [\"sample-server\"],")
        .Replace("\"slug\": \"migrate-the-callers\",", "\"slug\": \"migrate-the-callers\", \"repos\": [\"sample-client\"],");

    [Fact]
    public async Task SeriesOverServerAndClient_RunEndsWithoutCode_OnePrPerRepositoryCrossLinked()
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
            .EnqueueText(OneSpecEach)
            .EnqueueThrow(new MasterBudgetExhaustedException("per-pipeline cost budget exhausted: 15.02 USD of 15.00 USD spent"));

        var runner = new PipelineRunner(harness.Services) { ReposOverride = Repos(), DoneStatus = "done", FailedStatus = "failed" };
        await runner.RunAsync("code");

        var pipeline = runner.LastContext!;
        SpecPullRequests.Of(pipeline).Should().BeEquivalentTo(
            new Dictionary<string, string> { ["sample-server"] = ServerPr, ["sample-client"] = ClientPr });
        pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests)
            .Where(o => o.Status == OpenStatus.Opened).Select(o => (o.RepoName, o.Url))
            .Should().BeEquivalentTo([("sample-server", (string?)ServerPr), ("sample-client", (string?)ClientPr)],
                "a spec-only repository's pull request is listed as opened; the worker has none");
        pullRequests.Bodies[ServerPr].Should().Contain(ClientPr, "the cross-link reaches the spec PRs")
            .And.NotContain(SpecPrBody.SiblingMarker);
        pullRequests.Bodies[ClientPr].Should().Contain(ServerPr);
    }

    private static IReadOnlyList<RepoConnection> Repos() =>
    [
        new() { Name = "sample-server", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-server" },
        new() { Name = "sample-client", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-client" },
        new() { Name = "sample-worker", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-worker" },
    ];
}
