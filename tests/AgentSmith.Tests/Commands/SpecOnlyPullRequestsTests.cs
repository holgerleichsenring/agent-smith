using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Tests.TestHelpers;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-12-3a36c/f5f7e: a series repository's spec PR is the run's work whether or not it gained code.</summary>
public sealed class SpecOnlyPullRequestsTests
{
    private const string Url = "https://stub.test/sample-worker/pull/7";
    private const string ClientUrl = "https://stub.test/sample-client/pull/3";
    private static readonly RepoConnection[] Repos = [new() { Name = "sample-worker" }, new() { Name = "sample-client" }];

    private readonly Mock<ISourceProvider> _provider = new();
    private readonly SpecOnlyPullRequests _sut;

    public SpecOnlyPullRequestsTests()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        _sut = new SpecOnlyPullRequests(factory.Object, new PullRequestOutcomePublisher(EventTestStubs.NoOp), TestGit.Delivery, NullLogger<SpecOnlyPullRequests>.Instance);
    }

    [Fact]
    public async Task KeepAsync_SpecRepoSkipped_IsOpenedAndTheBodyRefreshedOnce()
    {
        var kept = await _sut.KeepAsync(LegacyPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges)], "banner body", false, CancellationToken.None);

        kept.Should().Equal(new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened));
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(Url, "banner body", It.IsAny<CancellationToken>()), Times.Once);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KeepAsync_SpecRepoAlreadyOpened_LeavesItAlone()
    {
        var kept = await _sut.KeepAsync(LegacyPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened)], "banner body", true, CancellationToken.None);

        kept.Should().BeEmpty();
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KeepAsync_NoSpecPullRequest_KeepsNothing()
    {
        var kept = await _sut.KeepAsync(new PipelineContext(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges)], "banner body", false, CancellationToken.None);

        kept.Should().BeEmpty();
    }

    [Fact]
    public async Task KeepAsync_TwoSpecOnlyReposAndReady_KeepsBothAndPromotesThem()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecPullRequestUrls,
            new Dictionary<string, string> { ["sample-worker"] = Url, ["sample-client"] = ClientUrl });

        var kept = await _sut.KeepAsync(pipeline, Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges),
             new OpenedPullRequest("sample-client", null, OpenStatus.SkippedNoChanges)], "body", true, CancellationToken.None);

        kept.Select(k => k.Url).Should().BeEquivalentTo([Url, ClientUrl]);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(Url, It.IsAny<CancellationToken>()), Times.Once);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(ClientUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PipelineContext LegacyPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecRepo, "sample-worker");
        pipeline.Set(ContextKeys.SpecPullRequestUrl, Url);
        return pipeline;
    }

    // 2026-11-27-ecf2c: with code in another repository, a branch carrying only the spec record keeps no pull request.
    // The record's done file and the merge=union line it writes into the root .gitattributes.
    private const string RecordOnly = "diff --git a/.agentsmith/specs/done/s1.yaml b/.agentsmith/specs/done/s1.yaml\n+++ b/.agentsmith/specs/done/s1.yaml\n+spec: s1\n"
        + "diff --git a/.gitattributes b/.gitattributes\n+++ b/.gitattributes\n+.agentsmith/contexts/*/done.md merge=union\n";
    private const string Source = "diff --git a/src/Fix.cs b/src/Fix.cs\n+++ b/src/Fix.cs\n+fixed\n" + RecordOnly;

    [Fact]
    public async Task KeepAsync_CodeElsewhereAndARecordOnlyBranch_LeavesItsPullRequestAlone()
    {
        var opened = WorkerCodeClientSkipped();

        var kept = await _sut.KeepAsync(TwoRepoPipeline(), Repos, opened, "body", true, CancellationToken.None, Code(new BranchSandbox(RecordOnly)));

        kept.Should().BeEmpty();
        opened[1].Should().Be(new OpenedPullRequest("sample-client", null, OpenStatus.SkippedNoChanges));
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(ClientUrl, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(ClientUrl, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KeepAsync_CodeElsewhereAndABranchCarryingSource_KeepsIt()
    {
        var kept = await _sut.KeepAsync(TwoRepoPipeline(), Repos, WorkerCodeClientSkipped(), "body", true, CancellationToken.None,
            Code(new BranchSandbox(Source)));

        kept.Should().Equal(new OpenedPullRequest("sample-client", ClientUrl, OpenStatus.Opened));
        _provider.Verify(p => p.MarkPullRequestReadyAsync(ClientUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KeepAsync_CodeElsewhereAndNoBaseRung_KeepsIt()
    {
        var kept = await _sut.KeepAsync(TwoRepoPipeline(), Repos, WorkerCodeClientSkipped(), "body", false, CancellationToken.None,
            Code(new BranchSandbox(RecordOnly, hasBase: false)));

        kept.Select(k => k.Url).Should().Equal(ClientUrl);
    }

    [Fact]
    public async Task KeepAsync_NoCodeAnywhere_KeepsBothAsBefore()
    {
        var kept = await _sut.KeepAsync(TwoRepoPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges),
             new OpenedPullRequest("sample-client", null, OpenStatus.SkippedNoChanges)], "body", true, CancellationToken.None,
            new SpecOnlyPullRequests.RunCode(false, new Dictionary<string, (string, ISandbox)> { ["sample-client"] = ("sample-client", new BranchSandbox(RecordOnly)) }));

        kept.Select(k => k.Url).Should().BeEquivalentTo([Url, ClientUrl]);
    }

    private static List<OpenedPullRequest> WorkerCodeClientSkipped() =>
        [new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened), new OpenedPullRequest("sample-client", null, OpenStatus.SkippedNoChanges)];

    private static SpecOnlyPullRequests.RunCode Code(ISandbox client) =>
        new(true, new Dictionary<string, (string, ISandbox)> { ["sample-client"] = ("sample-client", client) });

    private static PipelineContext TwoRepoPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecPullRequestUrls, new Dictionary<string, string> { ["sample-worker"] = Url, ["sample-client"] = ClientUrl });
        return pipeline;
    }

    /// <summary>A branch whose base is origin/main (or none) and whose diff against it is <paramref name="diff"/>.</summary>
    private sealed class BranchSandbox(string diff, bool hasBase = true) : ISandbox
    {
        public string JobId => "sample-client";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var args = step.Args ?? [];
            var (exit, output) = args.Contains("symbolic-ref") ? (hasBase ? 0 : 128, hasBase ? "origin/main" : "fatal")
                : args[0] == "diff" && args[^1] == "origin/main" && hasBase ? (0, diff)
                : args[0] == "diff" && args[^1] == "HEAD" ? (0, diff)
                : args[0] == "log" ? (0, string.Empty)
                : (128, "fatal: bad revision");
            return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
