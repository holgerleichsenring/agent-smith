using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Rework.Records.PrReviewHostFakes;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-26-5201f: whether a pull request's host still needs a person's approval — GitHub's reviewDecision, GitLab's approvals
/// left — as the Azure reviewer policy says it (AzureReposApprovalTests), and how the record carries it.
/// </summary>
public sealed class PrApprovalRequiredTests
{
    private const string GitLabMr = "https://gitlab.example/o/r/-/merge_requests/3";

    [Theory]
    [InlineData("\"REVIEW_REQUIRED\"", "OPEN", true)]
    [InlineData("\"CHANGES_REQUESTED\"", "OPEN", true)]
    [InlineData("\"APPROVED\"", "OPEN", false)]
    [InlineData("null", "OPEN", false)]
    [InlineData("\"REVIEW_REQUIRED\"", "MERGED", null)]
    public async Task GitHub_ReviewDecision_SaysWhetherARequiredReviewWaits(string decision, string state, bool? expected)
    {
        var reply = $$"""
            { "data": { "viewer": { "login": "agent" }, "repository": { "pullRequest": {
              "number": 4, "state": "{{state}}", "reviewDecision": {{decision}}, "createdAt": "2026-10-01T08:00:00Z", "mergedAt": null, "closedAt": null,
              "author": { "login": "agent" }, "latestReviews": { "nodes": [] } } } } }
            """;

        var read = await new GitHubPrReviewStateReader(GitHubReplying(reply), GitHubRepoAddress.Parse("https://github.com/o/r"))
            .ReadAsync("https://github.com/o/r/pull/4", CancellationToken.None);

        read.ApprovalRequired.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{ "approvals_required": 2, "approvals_left": 1, "approved": false }""", "opened", true)]
    [InlineData("""{ "approvals_required": 1, "approvals_left": 0, "approved": true }""", "opened", false)]
    [InlineData("""{ "approved": false }""", "opened", null)]
    [InlineData("""{ "approvals_left": 1 }""", "merged", null)]
    public async Task GitLab_ApprovalsLeft_SaysWhetherAnApprovalRuleWaits(string approvals, string state, bool? expected)
    {
        var mr = $$"""{ "iid": 3, "state": "{{state}}", "sha": "head1", "created_at": "2026-10-01T08:00:00Z", "author": { "id": 2 } }""";
        var read = await new GitLabMrStateReader("https://gitlab.example", "o%2Fr", "t", "https://gitlab.example/o/r", GitLabAnswering(path =>
                path.EndsWith("/api/v4/user") ? """{ "id": 1 }"""
                : path.EndsWith("/notes") ? "[]"
                : path.EndsWith("/approvals") ? approvals
                : mr), NullLogger.Instance)
            .ReadAsync(GitLabMr, CancellationToken.None);

        read.ApprovalRequired.Should().Be(expected);
    }

    [Fact]
    public void GitLab_Left_ReadsOnlyANumber()
    {
        GitLabMrApprovals.Left(JsonDocument.Parse("""{ "approvals_left": 2 }""").RootElement).Should().BeTrue();
        GitLabMrApprovals.Left(JsonDocument.Parse("""{ "approvals_left": "2" }""").RootElement).Should().BeNull();
        GitLabMrApprovals.Left(JsonDocument.Parse("[]").RootElement).Should().BeNull();
    }

    [Fact]
    public void OpenState_MapsTheApprovalOfAnOpenPullRequestOnly()
    {
        var record = new PrReviewRecord { CheckedAtTicks = 5 };
        PrReviewRecordOpenState.Map(Read(PrReviewStates.Open) with { ApprovalRequired = true, HasConflicts = true, IsDraft = true }, record);
        (record.ApprovalRequired, record.HasConflicts, record.ConflictsAtTicks, record.IsDraft).Should().Be((false, true, 5L, true),
            "a draft cannot be completed, so no approval is asked of anyone yet");

        PrReviewRecordOpenState.Map(Read(PrReviewStates.Open) with { ApprovalRequired = true }, record);
        record.ApprovalRequired.Should().BeTrue();
        PrReviewRecordOpenState.Map(Read(PrReviewStates.Open) with { ApprovalRequired = null, IsDraft = true }, record);
        record.ApprovalRequired.Should().BeNull("an undecided read stays undecided");

        PrReviewRecordOpenState.Map(Read(PrReviewStates.Merged) with { ApprovalRequired = true, HasConflicts = true, IsDraft = true }, record);
        (record.ApprovalRequired, record.HasConflicts, record.ConflictsAtTicks, record.IsDraft).Should().Be(((bool?)null, (bool?)null, (long?)null, false));
    }

    [Fact]
    public void Copy_KeepsTheApprovalOnAnUndecidedRead_ForgetsItOnAHeadMove_AndClearsItWhenEnded()
    {
        var stored = new PrReviewRecord { State = PrReviewStates.Open, HeadSha = "a", ApprovalRequired = true };
        AgentSmith.Infrastructure.Persistence.Repositories.PrReviewRecordCopy.Record(Open("a", null), stored);
        stored.ApprovalRequired.Should().BeTrue("the same head keeps what the host said last");

        AgentSmith.Infrastructure.Persistence.Repositories.PrReviewRecordCopy.Record(Open("a", false), stored);
        stored.ApprovalRequired.Should().BeFalse();

        AgentSmith.Infrastructure.Persistence.Repositories.PrReviewRecordCopy.Record(Open("b", null), stored);
        stored.ApprovalRequired.Should().BeNull("a push may reset the votes");

        stored.ApprovalRequired = true;
        AgentSmith.Infrastructure.Persistence.Repositories.PrReviewRecordCopy.Record(
            new PrReviewRecord { State = PrReviewStates.Merged, HeadSha = "b", CheckedAtTicks = 9 }, stored);
        stored.ApprovalRequired.Should().BeNull();
    }

    private static PrReviewRecord Open(string head, bool? approval) =>
        new() { State = PrReviewStates.Open, HeadSha = head, ApprovalRequired = approval, CheckedAtTicks = 9 };

    private static PrReviewState Read(string state) => new("4", state, DateTimeOffset.UnixEpoch, []);
}
