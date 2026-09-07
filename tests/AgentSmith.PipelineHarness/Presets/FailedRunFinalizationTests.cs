using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-07-f420 fast-tier end-to-end: the shape of run 2026-09-07T20-43-51-b7bc. A
/// two-phase derivation, the first phase green and accounted, the second phase's master
/// dying on the per-pipeline cost budget. The p0237 finalizer tail then reaches
/// CommitAndPR, which used to finalize the ticket "Completed across 1 repo(s)" with the
/// done status BEFORE the error path posted the failure. The partial work must still be
/// committed; the ticket must never read as completed.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FailedRunFinalizationTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private const string BudgetReason =
        "per-pipeline cost budget exhausted: 15.02 USD of 15.00 USD spent";

    [Fact]
    public async Task TwoPhaseDerivation_SecondMasterFails_TheTicketNeverReadsCompleted()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("No caller builds its own empty-payload check.");
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            // phase 1: green, verified, accounted
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict)
            // phase 2: the master dies on the money fence
            .EnqueueThrow(new MasterBudgetExhaustedException(BudgetReason));

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse("the second phase never happened");
        tickets.Finalized.Should().NotContain(f => f.Status == "done",
            "a failed run never moves the ticket to the done status, not even for a moment");
        tickets.Finalized.Should().NotContain(f => f.Comment.Contains("Completed", StringComparison.Ordinal),
            "the ticket author must never read a green summary of a red run");
        var last = tickets.Finalized.Should().ContainSingle("one comment: the failure").Subject;
        last.Status.Should().Be("failed");
        last.Comment.Should().Contain("Agent Smith — Failed").And.Contain("cost budget");

        var opened = runner.LastContext!.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        var pr = opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened,
            "the partial work is persisted as a draft PR").Subject;
        last.Comment.Should().Contain(pr.Url!, "the failure comment says where the partial work went");
        var bodies = runner.LastContext!.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies);
        bodies[pr.RepoName].Should().Contain("Run failed").And.Contain("cost budget")
            .And.NotContain("Completed");
        harness.StubSandboxFactory!.Spawned.Should().Contain(
            s => s.Sandbox.RanSteps.Any(step => step.Command == "git" && step.Args != null && step.Args.Contains("commit")),
            "p0237 stays: the partial work is committed");
        runner.LastContext!.Get<string>(ContextKeys.FailureReason).Should().Contain("cost budget");
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
}
