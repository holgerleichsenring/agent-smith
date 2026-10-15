using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59a: GET /api/tickets — one row per (project, ticket id), bucketed from its newest attempt.</summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketListReaderTests : IDisposable
{
    private const string R1 = "2026-10-13T09-00-00-0001";
    private const string R2 = "2026-10-13T10-00-00-0002";
    private const string R3 = "2026-10-13T11-00-00-0003";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketList_RunsOfOneTicket_AreOneRowWithAttemptsByRunId()
    {
        await _store.AddAsync(Run(R2, status: "failed"), Run(R1, status: "failed"), Step(R1), Step(R2));

        var row = (await ListAsync()).Tickets.Should().ContainSingle().Subject;

        row.AttemptCount.Should().Be(2);
        row.Attempts.Select(a => a.RunId).Should().Equal(R1, R2);
        row.NewestRunId.Should().Be(R2);
    }

    [Fact]
    public async Task TicketList_PrReviewRunOnTicket_IsNotAnAttempt()
    {
        await _store.AddAsync(Run(R1), Step(R1), Run(R2, pipeline: "pr-review"), Step(R2));

        var row = (await ListAsync()).Tickets.Should().ContainSingle().Subject;

        row.AttemptCount.Should().Be(1);
        row.NewestRunId.Should().Be(R1);
    }

    [Fact]
    public async Task TicketList_ResumedAttempt_KeepsItsNumber()
    {
        var promoted = Run(R2, status: "running");
        promoted.StartedAt = T.AddHours(-1); // RunStarted overwrote the reservation's StartedAt
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), promoted, Step(R2));

        var row = (await ListAsync()).Tickets.Single();

        row.Attempts.Select(a => a.RunId).Should().Equal(R1, R2);
        row.Bucket.Should().Be(TicketBucket.Working);
    }

    [Fact]
    public async Task TicketList_ParkedWithQueuePlace_IsWorkingNotNeedsYou()
    {
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1), Checkpoint(R1));
        _store.Positions[R1] = 2;

        var row = (await ListAsync()).Tickets.Single();

        row.Bucket.Should().Be(TicketBucket.Working);
        row.QueuePlace.Should().Be(2);
    }

    [Fact]
    public async Task TicketList_ParkedWithFreshRelaunchLease_IsWorkingNotNeedsYou()
    {
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1), Checkpoint(R1));
        _store.HeldLease = new StaleLease("alpha", new TicketId("42"), null, null, T.AddHours(3));

        var result = await ListAsync();

        result.Tickets.Single().Bucket.Should().Be(TicketBucket.Working);
        result.Counts.Queued.Should().Be(1);
        result.Counts.NeedsYou.Should().Be(0);
    }

    [Fact]
    public async Task TicketList_ParkedWithPendingCheckpoint_IsNeedsYouQuestion()
    {
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1), Checkpoint(R1));

        var row = (await ListAsync()).Tickets.Single();

        row.Bucket.Should().Be(TicketBucket.NeedsYou);
        row.Kind.Should().Be(TicketNeedsYouKind.Question);
        row.Since.Should().Be(T.AddMinutes(10));
    }

    [Fact]
    public async Task TicketList_NeedsYouQuestion_CarriesPendingQuestionWithRunId()
    {
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1), Checkpoint(R1));

        var question = (await ListAsync()).Tickets.Single().PendingQuestion!;

        question.RunId.Should().Be(R1);
        question.Question.Text.Should().Be("Which branch?");
        question.Question.DefaultAnswer.Should().Be("main");
    }

    [Fact]
    public async Task TicketList_ParkedWithoutCheckpoint_IsNeedsYouHandback()
    {
        await _store.AddAsync(Run(R1, status: "waiting_for_input"), Step(R1));

        var row = (await ListAsync()).Tickets.Single();

        row.Bucket.Should().Be(TicketBucket.NeedsYou);
        row.Kind.Should().Be(TicketNeedsYouKind.Handback);
        row.Since.Should().BeNull();
    }

    [Fact]
    public async Task TicketList_QueuedReservation_IsWorkingAndAnAttempt()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Run(R2, status: "queued"));

        var result = await ListAsync();

        var row = result.Tickets.Single();
        row.Bucket.Should().Be(TicketBucket.Working);
        row.AttemptCount.Should().Be(2);
        result.Counts.Queued.Should().Be(1);
    }

    [Fact]
    public async Task TicketList_ReservationCancelledBeforeStart_IsNotAnAttempt()
    {
        await _store.AddAsync(Run(R1, status: "failed"), Step(R1), Run(R2, status: "cancelled", cancelReason: "interrupted"));

        var row = (await ListAsync()).Tickets.Single();

        row.AttemptCount.Should().Be(1);
        row.NewestRunId.Should().Be(R1);
        row.Attempts.Should().OnlyContain(a => !a.EndedOnInfrastructure);
    }

    [Fact]
    public async Task TicketList_PrologueFailureOfReservation_IsAFailedAttempt()
    {
        await _store.AddAsync(Run(R1, status: "failed"));

        var row = (await ListAsync()).Tickets.Single();

        row.AttemptCount.Should().Be(1);
        row.LastOutcome.Should().Be("failed");
    }

    [Fact]
    public async Task TicketList_FinishedAttempt_IsIdleWithLastOutcome()
    {
        await _store.AddAsync(Run(R1, status: "success"), Step(R1));

        var row = (await ListAsync()).Tickets.Single();

        row.Bucket.Should().Be(TicketBucket.Idle);
        row.LastOutcome.Should().Be("success");
        row.Since.Should().Be(T.AddMinutes(30));
    }

    [Fact]
    public async Task TicketList_SameTicketIdTwoProjects_AreTwoTickets()
    {
        await _store.AddAsync(Run(R1), Step(R1), Run(R2, project: "beta"), Step(R2));

        (await ListAsync()).Tickets.Select(t => t.Project).Should().Equal("beta", "alpha");
    }

    private Task<TicketListView> ListAsync(TicketListRequest? request = null) =>
        _store.List.ReadAsync(request ?? new TicketListRequest(ProjectScope.Everything, null, null, null), CancellationToken.None);
}
