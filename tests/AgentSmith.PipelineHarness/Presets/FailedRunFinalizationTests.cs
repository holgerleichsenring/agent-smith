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
/// 2026-09-07-f420 fast-tier end-to-end: a derivation whose FIRST master dies on the
/// per-pipeline cost budget. The p0237 finalizer tail then reaches CommitAndPR, which used
/// to finalize the ticket "Completed across 1 repo(s)" with the done status BEFORE the
/// error path posted the failure (run 2026-09-07T20-43-51-b7bc). The partial work must
/// still be committed; the ticket must never read as completed.
/// <para>
/// p0439: b7bc's own shape — a VERIFIED first spec, then the budget — is no longer a
/// failed run but a delivered shortfall; ShortfallDeliveryTests owns it. A failed run is
/// one with nothing verified to deliver, which is what this case scripts.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FailedRunFinalizationTests
{
    private const string BudgetReason =
        "per-pipeline cost budget exhausted: 15.02 USD of 15.00 USD spent";

    [Fact]
    public async Task TwoSpecDerivation_FirstMasterFails_TheTicketNeverReadsCompleted()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoSpecJson)
            // spec 1: the master dies on the money fence before anything is verified
            .EnqueueThrow(new MasterBudgetExhaustedException(BudgetReason));

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse("nothing was verified, so nothing was delivered");
        tickets.Finalized.Should().NotContain(f => f.Status == "done",
            "a failed run never moves the ticket to the done status, not even for a moment");
        tickets.Finalized.Should().NotContain(f => f.Comment.Contains("Completed", StringComparison.Ordinal),
            "the ticket author must never read a green summary of a red run");
        var last = tickets.Finalized.Should().ContainSingle("one comment: the failure").Subject;
        last.Status.Should().Be("failed");
        last.Comment.Should().Contain("Agent Smith — Failed").And.Contain("cost budget");

        // 2026-10-12-3a36c: nothing ran past derivation, so the spec PR opened there is the
        // run's whole persisted work — no record-carrier commit is made on top of it.
        var opened = runner.LastContext!.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        var pr = opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened,
            "the spec PR is persisted as the failed run's draft").Subject;
        pr.Url.Should().Be(Application.Services.Specs.SpecPullRequests.Of(runner.LastContext!)[pr.RepoName]);
        last.Comment.Should().Contain(pr.Url!, "the failure comment says where the partial work went");
        var bodies = runner.LastContext!.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies);
        bodies[pr.RepoName].Should().Contain("Run failed").And.Contain("cost budget")
            .And.NotContain("Completed");
        harness.StubSandboxFactory!.Spawned.Should().NotContain(
            s => s.Sandbox.RanSteps.Any(step => step.Command == "git" && step.Args != null && step.Args.Contains("commit")
                && !step.Args.Any(a => a.StartsWith("spec: ", StringComparison.Ordinal))),
            "a run that did no work commits nothing beyond its series");
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
