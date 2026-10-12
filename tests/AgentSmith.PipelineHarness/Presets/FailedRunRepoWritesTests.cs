using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-3a36c fast-tier end-to-end: a three-repository run whose first master dies
/// before any phase did work. It used to push a result.md-only commit into every repository
/// — onto a reviewed PR, into a repository that had no branch, and a 'fix:' commit beside
/// the spec — although it changed nothing anywhere. A failed run commits only work it did:
/// the series commit at derivation, and nothing after it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class FailedRunRepoWritesTests
{
    private const string BudgetReason =
        "per-pipeline cost budget exhausted: 15.02 USD of 15.00 USD spent";

    private static IReadOnlyList<RepoConnection> ThreeRepos() =>
    [
        new() { Name = "sample-server", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-server" },
        new() { Name = "sample-client", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-client" },
        new() { Name = "sample-worker", Type = RepoType.Local, Path = "/tmp", Url = "https://stub.test/sample-worker" },
    ];

    [Fact]
    public async Task ThreeRepos_RunFailsBeforeAnyPhase_CommitsNothingAfterTheSeries()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.ChatClient
            .EnqueueScopeReply("no verdict") // unusable — the run keeps all three repos
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            .EnqueueThrow(new MasterBudgetExhaustedException(BudgetReason));

        var runner = new PipelineRunner(harness.Services)
        {
            ReposOverride = ThreeRepos(), DoneStatus = "done", FailedStatus = "failed",
        };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse("nothing was verified, so nothing was delivered");
        var sandboxes = harness.StubSandboxFactory!.Spawned.Select(s => s.Sandbox).ToList();
        sandboxes.Should().HaveCount(3);
        // The stub answers the staged-changes probe with "nothing", so the series itself
        // may not commit here; whatever does commit must be the series and nothing else.
        sandboxes.SelectMany(s => s.RanSteps).Where(IsGitCommit).Should().OnlyContain(
            s => IsSeriesCommit(s), "the series commit at derivation is the only work this run did");
        sandboxes.Where(s => !s.RanSteps.Any(IsGitCommit))
            .Should().AllSatisfy(s => s.RanSteps.Should().NotContain(st => IsGitPush(st),
                "a repository the run changed nothing in gets no branch pushed"));

        var pipeline = runner.LastContext!;
        var specPr = pipeline.Get<string>(ContextKeys.SpecPullRequestUrl);
        var opened = pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened)
            .Which.Url.Should().Be(specPr, "the spec PR is the failed run's persisted work");
        var comment = tickets.Finalized.Should().ContainSingle().Subject.Comment;
        comment.Should().Contain(specPr, "the failure comment names the spec PR");
    }

    private static bool IsGitCommit(Step step) =>
        step.Command == "git" && step.Args is { } a && a.Contains("commit");

    private static bool IsSeriesCommit(Step step) =>
        step.Args!.Any(a => a.StartsWith("spec: ", StringComparison.Ordinal));

    private static bool IsGitPush(Step step) =>
        step.Command == "git" && step.Args is { } a && a.Contains("push");

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
}
