using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-10-12-3a36c: a failed run commits into a repository only work it did there. No
/// record-carrier commit or PR holding only result.md, a WIP subject instead of 'fix:', and
/// the spec PR opened at derivation stays the run's persisted work.
/// </summary>
public sealed class CommitAndPRFailedRunTests
{
    private const string Reason = "per-pipeline cost budget exhausted";
    private const string SpecPr = "https://stub.test/sample-worker/pull/7";

    [Fact]
    public async Task CommitAndPR_FailedRunWithoutCode_OpensNoRecordPr()
    {
        var f = ThreeReposWithoutCode();
        f.Pipeline.Set(ContextKeys.FailureReason, Reason);

        var result = await f.RunAsync();

        result.Message.Should().Contain("Run failed").And.Contain("no PR opened");
        foreach (var repo in new[] { "sample-server", "sample-client", "sample-worker" })
            f.CommitMessagesIn(repo).Should().BeEmpty($"{repo} carries only the run record");
        VerifyNoPrCreated(f);
    }

    [Fact]
    public async Task CommitAndPR_FailedRun_FirstRepoWithoutPr_CreatesNoBranchOrPr()
    {
        var f = ThreeReposWithoutCode();
        f.Pipeline.Set(ContextKeys.FailureReason, Reason);

        await f.RunAsync();

        f.PushedIn("sample-server").Should().BeFalse("the first repo is no longer the record carrier");
        f.Provider.Verify(p => p.FindOpenPullRequestAsync(It.IsAny<Repository>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoPrCreated(f);
    }

    [Fact]
    public async Task CommitAndPR_FailedRunWithoutCode_KeepsSpecPrOutcomeOpened()
    {
        var f = ThreeReposWithoutCode();
        f.Pipeline.Set(ContextKeys.FailureReason, Reason);
        f.Pipeline.Set(ContextKeys.SpecRepo, "sample-worker");
        f.Pipeline.Set(ContextKeys.SpecPullRequestUrl, SpecPr);

        var result = await f.RunAsync();

        result.Message.Should().Contain(SpecPr);
        var opened = f.Pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        opened.Should().ContainSingle(o => o.Status == OpenStatus.Opened)
            .Which.Should().Be(new OpenedPullRequest("sample-worker", SpecPr, OpenStatus.Opened));
        f.Events.Events.OfType<PullRequestOutcomeEvent>().Last(e => e.Repo == "sample-worker")
            .Status.Should().Be("opened", "the projection keeps the last outcome per repo");
        f.Provider.Verify(p => p.UpdatePullRequestBodyAsync(
            SpecPr, It.Is<string>(b => b.StartsWith("> ⚠️ **Run failed**") && b.Contains(Reason)),
            It.IsAny<CancellationToken>()), Times.Once);
        f.CommitMessagesIn("sample-worker").Should().BeEmpty();
    }

    [Fact]
    public async Task CommitAndPR_FailedRunWithCode_UsesWipMessageWithFailedStep()
    {
        var f = new RepoCommitFixture()
            .AddRepo("sample-server", RepoCommitFixture.ResultMd)
            .AddRepo("sample-client", "src/Client.cs", RepoCommitFixture.ResultMd);
        f.Pipeline.Set(ContextKeys.FailureReason, Reason);
        f.Pipeline.Set(ContextKeys.FailedStepName, "[3/9] AgenticExecute");

        await f.RunAsync();

        var message = f.CommitMessagesIn("sample-client").Should().ContainSingle().Subject;
        message.Should().StartWith(WipCommit.Subject("run-1"))
            .And.Contain("Run-Id: run-1").And.Contain("Failed-Step: [3/9] AgenticExecute")
            .And.NotContain("fix:");
        f.CommitMessagesIn("sample-server").Should().BeEmpty("it changed nothing but the run record");
    }

    [Fact]
    public async Task CommitAndPR_GreenRunWithoutCode_KeepsRecordPr()
    {
        var f = ThreeReposWithoutCode();

        await f.RunAsync();

        f.CommitMessagesIn("sample-server").Should().ContainSingle()
            .Which.Should().StartWith("fix: Fix the bug", "p0235: a green run keeps one record PR");
        f.CommitMessagesIn("sample-client").Should().BeEmpty();
        f.Provider.Verify(p => p.CreatePullRequestAsync(
            It.Is<Repository>(r => r.RemoteUrl == "https://stub.test/sample-server"), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()), Times.Once);
    }

    // 2026-10-12-f5f7e: 3a36c's keep, generalised — a green run keeps a spec-only repository's spec PR too.
    [Fact]
    public async Task CommitAndPR_GreenRunSpecOnlyRepo_KeepsSpecPrOpened()
    {
        var f = new RepoCommitFixture()
            .AddRepo("sample-server", "src/Server.cs", RepoCommitFixture.ResultMd)
            .AddRepo("sample-client", RepoCommitFixture.ResultMd);
        f.Pipeline.Set(ContextKeys.SpecPullRequestUrls, new Dictionary<string, string> { ["sample-client"] = SpecPr });

        await f.RunAsync();

        var opened = f.Pipeline.Get<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests);
        opened.Should().Contain(new OpenedPullRequest("sample-client", SpecPr, OpenStatus.Opened));
        opened.Should().Contain(o => o.RepoName == "sample-server" && o.Status == OpenStatus.Opened);
        f.Pipeline.Get<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies)["sample-client"]
            .Should().Contain(Application.Services.Specs.SpecPrBody.SiblingMarker, "the cross-link reaches it");
        f.Provider.Verify(p => p.UpdatePullRequestBodyAsync(SpecPr, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        f.CommitMessagesIn("sample-client").Should().BeEmpty("it gained no code");
    }

    private static RepoCommitFixture ThreeReposWithoutCode() =>
        new RepoCommitFixture()
            .AddRepo("sample-server", RepoCommitFixture.ResultMd)
            .AddRepo("sample-client", RepoCommitFixture.ResultMd)
            .AddRepo("sample-worker", RepoCommitFixture.ResultMd);

    private static void VerifyNoPrCreated(RepoCommitFixture f) =>
        f.Provider.Verify(p => p.CreatePullRequestAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()), Times.Never);
}
