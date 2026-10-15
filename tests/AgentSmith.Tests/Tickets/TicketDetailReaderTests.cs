using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59a: GET /api/tickets/{project}/{ticketId} — attempts numbered by run id, other runs beside them.</summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketDetailReaderTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string R2 = "2026-10-13T10-00-00-0002";
    private const string R3 = "2026-10-13T11-00-00-0003";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketDetail_UnknownPair_Is404()
    {
        await _store.AddAsync(Run(R1, pipeline: "pr-review"));

        var result = await TicketQueryEndpoints.DetailAsync("alpha", "42", _store.Detail, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task TicketDetail_UrlEncodedTicketId_IsFound()
    {
        await _store.AddAsync(Run(R1, ticket: "team/42"), Step(R1));

        var result = await TicketQueryEndpoints.DetailAsync("alpha", "team%2F42", _store.Detail, CancellationToken.None);

        result.Should().BeOfType<Ok<TicketDetailView>>().Which.Value!.Summary.TicketId.Should().Be("team/42");
    }

    [Fact]
    public async Task TicketDetail_DeletedRun_RenumbersTheAttempts()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Run(R2, status: "failed"), Step(R2),
            Run(R3), Step(R3));
        await _store.DeleteRunAsync(R1);

        var attempts = (await ReadAsync()).Attempts;

        attempts.Select(a => (a.Number, a.RunId)).Should().Equal((1, R2), (2, R3));
    }

    [Fact]
    public async Task TicketDetail_CarriesTheBranchName()
    {
        await _store.AddAsync(Run(R1), Step(R1));

        (await ReadAsync()).Branch.Should().Be("agent-smith/42");
    }

    [Fact]
    public async Task TicketDetail_OtherRuns_HoldOtherPipelinesAndNeverStartedRows()
    {
        await _store.AddAsync(Run(R1, status: "cancelled"), Run(R2), Step(R2), Run(R3, pipeline: "pr-review"));

        var view = await ReadAsync();

        view.Attempts.Should().ContainSingle().Which.RunId.Should().Be(R2);
        view.OtherRuns.Select(o => (o.RunId, o.NeverStarted)).Should().Equal((R1, true), (R3, false));
    }

    [Fact]
    public async Task TicketDetail_Attempt_CarriesInfrastructureEndPullRequestsAndSpecs()
    {
        var reaped = Run(R1, status: "cancelled", cancelReason: "stale-lease-reaped");
        var worked = Run(R2);
        worked.PullRequestsJson = """[{"repo":"api","status":"opened","url":"https://git/pr/1"}]""";
        await _store.AddAsync(reaped, Step(R1), worked, Step(R2), Spec(R2, "s1", "done"));

        var attempts = (await ReadAsync()).Attempts;

        attempts[0].EndedOnInfrastructure.Should().BeTrue();
        attempts[1].PullRequests.Should().ContainSingle().Which.Url.Should().Be("https://git/pr/1");
        attempts[1].ExecutedSpecs.Should().ContainSingle().Which.SpecId.Should().Be("s1");
    }

    // 2026-10-13-9a59n: the detail's and the list's marks read the sandbox arm from the attempt's last launch.
    [Fact]
    public async Task TicketDetailAndList_SandboxVanishedInLastLaunch_MarkInfrastructureEnd()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Run(R2, status: "failed"), Step(R2),
            new RunSegment { RunId = R1, EventSeq = 1, StartedAt = T, EndSeq = 9, EndedAt = T },
            new RunSegment { RunId = R1, EventSeq = 10, StartedAt = T, EndSeq = 20, EndedAt = T },
            new RunSandbox { RunId = R1, Key = "api", RepoName = "api", EventSeq = 15, EndReason = "vanished" },
            new RunSegment { RunId = R2, EventSeq = 1, StartedAt = T, EndSeq = 9, EndedAt = T },
            new RunSegment { RunId = R2, EventSeq = 10, StartedAt = T, EndSeq = 20, EndedAt = T },
            new RunSandbox { RunId = R2, Key = "api", RepoName = "api", EventSeq = 5, EndReason = "vanished" });

        var attempts = (await ReadAsync()).Attempts;
        var row = (await _store.List.ReadAsync(new TicketListRequest(ProjectScope.Everything, null, null, null), CancellationToken.None)).Tickets.Single();

        attempts.Select(a => a.EndedOnInfrastructure).Should().Equal(true, false);
        row.Attempts.Select(a => a.EndedOnInfrastructure).Should().Equal(true, false);
    }

    [Fact]
    public async Task TicketDetail_Attempts_CarryCauseRevisionAndWebUrl()
    {
        // 2026-10-13-9a59d: each attempt names what started it and what it worked from; the row names
        // the newest attempt that has a record — a queued reservation has none yet.
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Run(R2), Step(R2), Run(R3, status: "queued"),
            new RunAttempt
            {
                RunId = R1, CauseChannel = AttemptChannels.Pickup, CauseActor = "dave", TrackerState = "Ready",
                WebUrl = "https://github.com/o/api/issues/42",
            },
            new RunAttempt
            {
                RunId = R2, CauseChannel = AttemptChannels.PullRequest, CauseActor = "alice", CauseAt = T,
                SourceKind = AttemptSourceKinds.Review, SourceUrl = "https://github.com/o/api/pull/4", SourceId = "PRR_1",
                TrackerState = "In Review", WebUrl = "https://github.com/o/api/issues/42", RevisionNumber = 2,
                RevisionCause = "pull request review", SeriesId = "2026-10-13-1234",
                CarriedSpecIdsJson = "[\"2026-10-13-1234a\"]", ApprovalAt = T.AddMinutes(-5),
            });

        var detail = await ReadAsync();

        detail.Attempts[1].Cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.PullRequest, "alice", T,
            new AttemptSource(AttemptSourceKinds.Review, "https://github.com/o/api/pull/4", "PRR_1")));
        detail.Attempts[1].Revision.Should().BeEquivalentTo(new TicketAttemptRevisionView(
            2, "pull request review", "2026-10-13-1234", ["2026-10-13-1234a"], T.AddMinutes(-5)));
        detail.Attempts[0].Revision.Should().BeNull("the first attempt published no revision");
        detail.Attempts[2].Cause.Should().BeNull("a queued reservation has not fetched its ticket");
        detail.Summary.Cause!.Channel.Should().Be(AttemptChannels.PullRequest);
        detail.Summary.TrackerState.Should().Be("In Review");
        detail.Summary.WebUrl.Should().Be("https://github.com/o/api/issues/42");
    }

    private async Task<TicketDetailView> ReadAsync() =>
        (await _store.Detail.ReadAsync("alpha", "42", CancellationToken.None))!;
}
