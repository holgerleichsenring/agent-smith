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

    private async Task<TicketDetailView> ReadAsync() =>
        (await _store.Detail.ReadAsync("alpha", "42", CancellationToken.None))!;
}
