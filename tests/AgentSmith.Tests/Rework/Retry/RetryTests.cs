using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Rework.Retry;

/// <summary>
/// 2026-10-13-9a59n: Retry starts an unchanged attempt only after an infrastructure end, through the rework
/// queue and the real rework entry, with the retrying person as its cause; every other end answers 409 with one
/// body that points at the ticket conversation.
/// </summary>
public sealed class RetryTests : IDisposable
{
    private const string Cancelled = "cancelled";

    private readonly RetryHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Retry_InterruptedRun_StartsOneAttempt_CauseRetry()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");

        await StartsOneAttemptAsync("r-1");

        var request = (await _h.RequestsAsync()).Single();
        var cause = await _h.CauseOfNextAttemptAsync("r-2");
        cause.Channel.Should().Be(AttemptChannels.Retry);
        cause.Actor.Should().Be(RetryHarness.Principal);
        cause.At.Should().Be(request.RequestedAt);
    }

    [Fact]
    public async Task Retry_StaleLeaseReaped_StartsOneAttempt()
    {
        await _h.AttemptAsync("r-1", Cancelled, "stale-lease-reaped");

        await StartsOneAttemptAsync("r-1");
    }

    [Fact]
    public async Task Retry_SandboxVanished_StartsOneAttempt()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed, null,
            new RunSegment { RunId = "r-1", EventSeq = 1, StartedAt = TicketStoreHarness.T, EndSeq = 9, EndedAt = TicketStoreHarness.T },
            new RunSegment { RunId = "r-1", EventSeq = 10, StartedAt = TicketStoreHarness.T, EndSeq = 30, EndedAt = TicketStoreHarness.T },
            new RunSandbox { RunId = "r-1", Key = "api", RepoName = "api", EventSeq = 12, EndReason = SandboxEndReasons.Vanished });

        await StartsOneAttemptAsync("r-1");
    }

    [Fact]
    public async Task Retry_VerdictPark_409NamesConversation()
    {
        await _h.AttemptAsync("r-1", RunStatuses.WaitingForInput);

        var refusal = await RefusedAsync("r-1");

        refusal.Reason.Should().Be(RetryRefusals.HandedBack, "a hand-back gets the conversation answer, not 'not finished'");
        refusal.Text.Should().Contain("ticket conversation");
    }

    [Fact]
    public async Task Retry_FailedOnVerdict_409NamesConversation()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed);

        var refusal = await RefusedAsync("r-1");

        refusal.Reason.Should().Be(RetryRefusals.NotInfrastructure);
        refusal.Text.Should().Be(RetryRefusals.NotInfrastructureText);
    }

    [Fact]
    public async Task Retry_NotLatestAttempt_409NamesNewerAttempt()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        await _h.AttemptAsync("r-2", RunStatuses.Failed);

        var refusal = await RefusedAsync("r-1");

        refusal.Reason.Should().Be(RetryRefusals.NotLatest);
        refusal.Text.Should().Contain("r-2");
    }

    [Fact]
    public async Task Retry_NewerQueuedReservation_409NamesIt()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        await _h.AddAsync(TicketStoreHarness.Run("r-2", status: RunStatuses.Queued));

        var refusal = await RefusedAsync("r-1");

        refusal.Reason.Should().Be(RetryRefusals.NotLatest);
        refusal.Text.Should().Contain("r-2").And.Contain("queued");
    }

    [Fact]
    public async Task Retry_WhileAttemptStarting_409NamesFirstRequester()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        (await _h.RetryAsync("r-1", "first-person")).Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        await _h.InScopeAsync(sp => sp.GetRequiredService<ActiveRunRepository>().TryClaimAsync("alpha", new TicketId("42"), default));

        var refusal = await RefusedAsync("r-1", "second-person");

        refusal.Reason.Should().Be(RetryRefusals.Starting);
        refusal.Text.Should().Contain("first-person");
        (await _h.RequestsAsync()).Single().RequestedBy.Should().Be("first-person", "a refused request records nothing");
    }

    [Fact]
    public async Task Retry_AfterRefusedFirstRequest_UpsertsAndEnqueues()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        _h.Tracker.RefusesMoves = true;
        await _h.RetryAsync("r-1", "first-person");
        (await _h.HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        _h.Launch.VerifyNoOtherCalls();
        _h.Tracker.Comments.Should().ContainSingle(c => c.Comment.Contains("no move to 'open'"), "the refusal is said on the ticket");

        _h.Tracker.RefusesMoves = false;
        var again = await _h.RetryAsync("r-1", "second-person");

        again.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        (await _h.RequestsAsync()).Should().ContainSingle().Which.RequestedBy.Should().Be("second-person");
        (await _h.NudgesAsync()).Should().ContainSingle(n => n.Channel == nameof(ReworkChannel.Retry));
        await _h.HandleNextAsync();
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Retry_ClosedTicket_409PointsAtTheConversation_RecordsNothing()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        _h.Tracker.Status = "Closed";
        _h.Tracker.IsClosed = true;

        var refusal = await RefusedAsync("r-1");

        refusal.Reason.Should().Be(RetryRefusals.ClosedForWork);
        refusal.Text.Should().Be(RetryRefusals.ClosedForWorkText);
        (await _h.RequestsAsync()).Should().BeEmpty();
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Retry_OwnFailedStatusInClosedCategory_StartsOneAttempt()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        _h.Tracker.IsClosed = true; // the enforcer's own move to failed_status, which the tracker counts closed

        await StartsOneAttemptAsync("r-1");
    }

    [Fact]
    public async Task Retry_TrackerUnreadable_503RecordsNothing()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        _h.Tracker.ReadFails = new InvalidOperationException("tracker down");

        var result = await _h.RetryAsync("r-1");

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        (await _h.RequestsAsync()).Should().BeEmpty();
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(RunStatuses.WaitingForInput, null, RetryRefusals.HandedBack)]
    [InlineData(RunStatuses.Failed, null, RetryRefusals.NotInfrastructure)]
    [InlineData("running", null, RetryRefusals.NotFinished)]
    [InlineData(Cancelled, "operator", RetryRefusals.NotInfrastructure)]
    public async Task Retry_Every409_CarriesReasonCodeAndText(string status, string? reason, string code)
    {
        await _h.AttemptAsync("r-1", status, reason);

        var refusal = await RefusedAsync("r-1");

        refusal.Should().BeEquivalentTo(new { Reason = code, Project = "alpha", TicketId = "42" });
        refusal.Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Retry_UnknownRun_404()
    {
        (await _h.RetryAsync("nope")).Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task RunDeletion_DeletesRetryRequest()
    {
        await _h.AttemptAsync("r-1", Cancelled, "interrupted");
        await _h.RetryAsync("r-1");

        await _h.InScopeAsync(sp => sp.GetRequiredService<RunDeletionRepository>().DeleteAsync("r-1", default));

        (await _h.RequestsAsync()).Should().BeEmpty();
    }

    private async Task StartsOneAttemptAsync(string runId)
    {
        var result = await _h.RetryAsync(runId);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        (await _h.NudgesAsync()).Should().ContainSingle(n => n.Origin == (int)ReworkNudgeOrigin.Operator && n.Channel == "Retry");
        (await _h.HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
        _h.Tracker.Moves.Should().Equal(["open"], "the entry moves the failed ticket into its trigger status");
    }

    private async Task<RetryRefusalView> RefusedAsync(string runId, string principal = RetryHarness.Principal) =>
        (await _h.RetryAsync(runId, principal)).Should().BeOfType<Conflict<RetryRefusalView>>().Which.Value!;
}
