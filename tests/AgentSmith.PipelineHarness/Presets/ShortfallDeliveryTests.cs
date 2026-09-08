using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0439 fast-tier end-to-end: the shape of run 2026-09-08T11-28-15-989e. A two-phase
/// derivation, the first phase green, verified and recorded, the second phase's master
/// dying on the per-pipeline cost budget. The run built and verified half of its contract;
/// that half is DELIVERED — a ready pull request naming what was left, the ticket finalized
/// with the done status — instead of a failed run whose work nobody is pointed at.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ShortfallDeliveryTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    private const string BudgetReason =
        "per-pipeline cost budget exhausted: 8.02 USD of 8.00 USD spent";

    [Fact]
    public async Task TwoPhaseDerivation_SecondMasterFailsOnTheBudget_TheFirstPhaseIsDelivered()
    {
        var tickets = new RecordingTicketProvider();
        var pullRequests = new RecordingSourceProvider();
        await using var harness = BuildHarness(tickets, pullRequests);
        // The harness accountant cites the first file it sees for every criterion; withhold
        // phase 2's so the phase is not found already satisfied on entry and its master runs.
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("No caller builds its own empty-payload check.");
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            // phase 1: green, verified, accounted, recorded
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict)
            // phase 2: the master dies on the money fence before it does anything
            .EnqueueThrow(new MasterBudgetExhaustedException(BudgetReason));

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue("the run delivered what it built and verified");
        var finalized = tickets.Finalized.Should().ContainSingle("one comment: the delivery").Subject;
        finalized.Status.Should().Be("done", "a delivered shortfall counts as done — operator ruling");
        finalized.Comment.Should().Contain("shortfall")
            .And.Contain("Not delivered")
            .And.Contain("cost budget")
            .And.NotContain("Agent Smith — Failed");

        var opened = runner.LastContext!.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        var pr = opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened).Subject;
        pullRequests.Drafts.Should().NotContain(pr.Url!, "the delivered phase is verified: the PR is ready, not a draft");
        var bodies = runner.LastContext!.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies);
        bodies[pr.RepoName].Should().Contain("Not delivered")
            .And.Contain("cost budget")
            .And.Contain("What this run accounted for", "the finished phase's account is the delivery's evidence")
            .And.NotContain("Run failed")
            .And.NotContain("DO NOT MERGE");
        finalized.Comment.Should().Contain(pr.Url!);

        var shortfall = Contracts.Specs.RunShortfall.DeliveredOn(runner.LastContext!);
        shortfall.Should().NotBeNull("the executor and the use case read the delivery off the context");
        shortfall!.Delivered.Select(p => p.PhaseId).Should().HaveCount(1);
        shortfall.NotDelivered.Select(p => p.PhaseId).Should().HaveCount(1);
        shortfall.Reason.Should().Contain("cost budget");
        runner.LastContext!.Has(ContextKeys.FailedStepName).Should().BeFalse(
            "the error path never ran: no failure comment, no failed status, no WIP persist");
    }

    [Fact]
    public async Task TwoPhaseDerivation_FirstMasterFails_StaysAFailedRun()
    {
        // The other half of the keystone: nothing was verified, so nothing is delivered —
        // the 2026-09-07-f420 shape is untouched. Draft PR with the failure banner, one
        // failure comment with the failed status.
        var tickets = new RecordingTicketProvider();
        var pullRequests = new RecordingSourceProvider();
        await using var harness = BuildHarness(tickets, pullRequests);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            .EnqueueThrow(new MasterBudgetExhaustedException(BudgetReason));

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeFalse("no phase was verified, so nothing was delivered");
        var last = tickets.Finalized.Should().ContainSingle().Subject;
        last.Status.Should().Be("failed");
        last.Comment.Should().Contain("Agent Smith — Failed");
        var opened = runner.LastContext!.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        var pr = opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened).Subject;
        pullRequests.Drafts.Should().Contain(pr.Url!, "a failed run's PR stays a draft");
        Contracts.Specs.RunShortfall.DeliveredOn(runner.LastContext!).Should().BeNull();
    }

    private static RealCompositionHarness BuildHarness(
        RecordingTicketProvider tickets, RecordingSourceProvider pullRequests) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
            services.RemoveAll<ISourceProviderFactory>();
            services.AddSingleton<ISourceProviderFactory>(new RecordingSourceProviderFactory(pullRequests));
        });

    /// <summary>The stub source provider, recording whether each pull request was opened
    /// as a draft and which ones were later taken out of draft.</summary>
    private sealed class RecordingSourceProvider : ISourceProvider
    {
        private readonly ISourceProvider _inner = new StubSourceProviderFactory().Create(new RepoConnection());
        private readonly HashSet<string> _drafts = [];

        /// <summary>Pull requests that are drafts now: opened as one and never marked ready.</summary>
        public IReadOnlyCollection<string> Drafts { get { lock (_drafts) return [.. _drafts]; } }

        public string ProviderType => _inner.ProviderType;

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) => _inner.ProbeAsync(cancellationToken);

        public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
            _inner.CheckoutAsync(branch, cancellationToken);

        public async Task<string> CreatePullRequestAsync(
            Repository repository, string title, string description,
            CancellationToken cancellationToken, TicketId? linkedTicketId = null, bool isDraft = false)
        {
            var url = await _inner.CreatePullRequestAsync(repository, title, description, cancellationToken, linkedTicketId, isDraft);
            lock (_drafts) { if (isDraft) _drafts.Add(url); else _drafts.Remove(url); }
            return url;
        }

        public Task<string?> FindOpenPullRequestAsync(Repository repository, CancellationToken cancellationToken) =>
            _inner.FindOpenPullRequestAsync(repository, cancellationToken);

        public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
            _inner.TryReadFileAsync(path, cancellationToken);

        public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
            _inner.ListDirectoryAsync(path, cancellationToken);

        public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken) =>
            _inner.UpdatePullRequestBodyAsync(prUrl, newBody, cancellationToken);

        public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken)
        {
            lock (_drafts) _drafts.Remove(prUrl);
            return _inner.MarkPullRequestReadyAsync(prUrl, cancellationToken);
        }

        public Task<PullRequestCompletion> CompletePullRequestAsync(
            string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
            _inner.CompletePullRequestAsync(prUrl, sourceBranch, cancellationToken);
    }

    private sealed class RecordingSourceProviderFactory(RecordingSourceProvider provider) : ISourceProviderFactory
    {
        public ISourceProvider Create(RepoConnection config) => provider;
    }
}
