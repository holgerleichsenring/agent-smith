using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Segments;

/// <summary>
/// 2026-10-13-9a59j: a run's segments read back with their call time, the calls no segment holds, and each
/// park's waiter — a question answered from the inbox, a hand-back answered by the next fetched attempt's cause,
/// a capacity requeue with no park, an unrecorded park as kind null. Seeded rows over a migrated SQLite store.
/// </summary>
public sealed class RunSegmentsReaderTests : IDisposable
{
    private const string RunId = "2026-10-13T0900-a";
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly RunSegmentsReader _reader = new(new RunParkReader());

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunSegments_ModelTimeByTrailPosition_CountsUnattributedOnce()
    {
        await SeedAsync(Run(RunId), Segment(1, 0, 10, 20, "waiting_for_input"), Segment(30, 40),
            Call(2, 1_000), Call(9, 500), Call(31, 2_000), Call(null, 700), Call(null, 300));

        var read = await ReadAsync();

        read.Segments.Select(s => s.ModelMs).Should().Equal(1_500L, 2_000L);
        read.UnattributedCalls.Should().Be(2);
    }

    [Fact]
    public async Task RunSegments_OverlappingCalls_ModelTimeMayExceedWallClock()
    {
        await SeedAsync(Run(RunId), Segment(1, 0, 10, 1, "success"),
            Call(2, 50_000), Call(3, 50_000), Call(4, 50_000));

        var segment = (await ReadAsync()).Segments.Single();

        segment.ModelMs.Should().Be(150_000);
        (segment.EndedAt!.Value - segment.StartedAt).TotalMilliseconds.Should().BeLessThan(segment.ModelMs,
            "concurrent sub-agent calls each count in full");
    }

    [Fact]
    public async Task RunSegments_QuestionPark_NamesAskedFromEntryAndAnswerFromInbox()
    {
        await SeedAsync(Run(RunId, ticketId: "42"), Segment(1, 0, 10, 5, "waiting_for_input"), Segment(20, 30),
            Entry(RunConversationKinds.Question, 8, 4, "q1"),
            new DialogueAnswerEntry { DialogueJobId = RunId, QuestionId = "q1", Answer = "yes", AnsweredBy = "alice", AnsweredAt = T.AddMinutes(25) });

        var segments = (await ReadAsync()).Segments;

        segments[0].Park.Should().Be(new RunComputeParkView(
            RunParkKinds.Question, T.AddMinutes(4), "q1", T.AddMinutes(25), "alice", T.AddMinutes(30)));
        segments[1].Park.Should().BeNull();
    }

    [Fact]
    public async Task RunSegments_HandbackPark_AnsweredByNextAttemptCause()
    {
        await SeedAsync(Run(RunId, ticketId: "42"), Segment(1, 0, 10, 5, "waiting_for_input"),
            Entry(RunConversationKinds.HandBack, 8, 4),
            Run("2026-10-13T1000-b", ticketId: "42"), Attempt("2026-10-13T1000-b", "bob", 90),
            Run("2026-10-13T1100-c", ticketId: "42"), Attempt("2026-10-13T1100-c", "carol", 120),
            Run("2026-10-13T1000-other", ticketId: "43"), Attempt("2026-10-13T1000-other", "dave", 60));

        var park = (await ReadAsync()).Segments.Single().Park;

        park.Should().Be(new RunComputeParkView(RunParkKinds.Handback, T.AddMinutes(4), null, T.AddMinutes(90), "bob", null));
    }

    [Fact]
    public async Task RunSegments_HandbackPark_NoNextAttempt_Unanswered()
    {
        await SeedAsync(Run(RunId, ticketId: "42"), Segment(1, 0, 10, 5, "waiting_for_input"),
            Entry(RunConversationKinds.HandBack, 8, 4));
        (await ReadAsync()).Segments.Single().Park.Should().Be(
            new RunComputeParkView(RunParkKinds.Handback, T.AddMinutes(4), null, null, null, null));

        await SeedAsync(Run("2026-10-13T1000-b", ticketId: "42", status: "queued"));

        (await ReadAsync()).Segments.Single().Park!.AnsweredAt.Should().BeNull(
            "a queued reservation has no cause until it fetches its ticket");
    }

    [Fact]
    public async Task RunSegments_TicketlessQuestionPark_ReadsUnknownPark()
    {
        await SeedAsync(Run(RunId), Segment(1, 0, 10, 5, "waiting_for_input"), Segment(20, 30));

        (await ReadAsync()).Segments[0].Park.Should().Be(
            new RunComputeParkView(null, null, null, null, null, T.AddMinutes(30)));
    }

    [Fact]
    public async Task RunSegments_QueuedEnd_IsNotAPark()
    {
        await SeedAsync(Run(RunId, ticketId: "42"), Segment(1, 0, 10, 5, "queued"), Segment(20, 30),
            Entry(RunConversationKinds.Question, 8, 4, "q1"));

        var segments = (await ReadAsync()).Segments;

        segments[0].EndStatus.Should().Be("queued");
        segments.Should().OnlyContain(s => s.Park == null);
    }

    private static Run Run(string id, string ticketId = "", string status = "running") => new()
    {
        Id = id, Project = "alpha", Pipeline = "code", TicketId = ticketId, Status = status, Trigger = "ticket", StartedAt = T,
    };

    private static RunSegment Segment(long seq, int startMinute, long? endSeq = null, int? endMinute = null, string? status = null) => new()
    {
        RunId = RunId, EventSeq = seq, StartedAt = T.AddMinutes(startMinute), EndSeq = endSeq,
        EndedAt = endMinute is { } m ? T.AddMinutes(m) : null, EndStatus = status,
    };

    private static RunLlmCall Call(long? seq, long durationMs) => new() { RunId = RunId, EventSeq = seq, DurationMs = durationMs };

    private static RunConversationEntry Entry(string kind, long seq, int minute, string? questionId = null) => new()
    {
        RunId = RunId, EventSeq = seq, Kind = kind, At = T.AddMinutes(minute), QuestionId = questionId,
    };

    private static RunAttempt Attempt(string runId, string actor, int minute) => new()
    {
        RunId = runId, CauseChannel = "ticket_comment", CauseActor = actor, CauseAt = T.AddMinutes(minute),
    };

    private async Task SeedAsync(params object[] rows)
    {
        await using var db = Context();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private async Task<RunSegmentsRead> ReadAsync()
    {
        await using var db = Context();
        return await _reader.ReadAsync(db, RunId, CancellationToken.None);
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
