using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-06-3d81 fast-tier end-to-end: the shape of local replay 2026-09-08T07-01-24-5556.
/// The master attempted a criterion the repository makes impossible, disposed it as
/// not_applicable with the evaluated meaning of not doing it — and nothing rendered that
/// answer where the ticket author reads. The run finished; the sentence of the ticket no
/// work could satisfy was invisible.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DeclinedCriterionTests
{
    private const string Reason =
        "the lint rules live in a shared config package this repository does not own and "
        + "cannot change; adopting them here would flag production code this run never touched";

    private const string DecliningVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"guarded","acceptance":[{"criterion":"An empty request body is answered with 400.","status":"met","evidence":"the guard"},{"criterion":"Existing callers stay unaffected.","status":"not_applicable","evidence":"""
        + "\"" + Reason + "\"}]}";

    [Fact]
    public async Task Harness_AMasterDeclinesOneCriterion_TheTicketAuthorReadsItOnTheTicketAndThePullRequest()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(DecliningVerdict);

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("a declined criterion with its reason satisfies the gate");
        var completed = tickets.Finalized.Should().ContainSingle("the run completed and said so once").Subject;
        completed.Comment.Should().Contain("Existing callers stay unaffected.")
            .And.Contain(Reason, "the ticket author reads which sentence no work could satisfy, and why");

        var opened = runner.LastContext!.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        var pr = opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened).Subject;
        var bodies = runner.LastContext!.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies);
        bodies[pr.RepoName].Should().Contain("Existing callers stay unaffected.").And.Contain(Reason,
            "the reviewer reads the declined criterion on the pull request");

        var store = harness.Services.GetRequiredService<IRunArtifactStore>();
        var resultMd = await store.ReadResultMarkdownAsync(runner.LastRunId!, CancellationToken.None);
        resultMd.Should().Contain(Reason, "the run record carries the declined criterion too");
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
}
