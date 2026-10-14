using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;
using RunSandbox = AgentSmith.Infrastructure.Persistence.Entities.RunSandbox;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-13-9a59i: a sandbox row says how it ended. The first end wins, a close lands on the latest row
/// created below its own trail position, and the run's end — bounded by its position — names a release
/// (park, requeue, end) or marks a row no close reached 'unobserved'. Replays with kept positions change
/// nothing. Proven through the real applier on SQLite.
/// </summary>
public sealed class SandboxEndProjectionTests : IDisposable
{
    private const string RunId = "run-1";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-13T09:00:00Z");

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly RunEventApplier _applier = RunEventAppliers.Default();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SandboxDispose_AfterVanish_KeepsVanished_FillsExitCode()
    {
        await ApplyAsync((1, Started()), (2, Created("api")),
            (3, Vanished("api", exitCode: null, oom: null)),
            (4, Disposed("api", "exited", 137, false)));

        var box = (await BoxesAsync()).Single();
        (box.EndReason, box.ExitCode, box.OomKilled).Should().Be(("vanished", 137, false));
        box.DisposedAt.Should().Be(T.AddMinutes(3), "the vanish closed the window first");
    }

    [Fact]
    public async Task SandboxVanish_OomKilled_EndsOomKilled()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Vanished("api", 137, true)));

        (await BoxesAsync()).Single().EndReason.Should().Be("oom-killed");
    }

    [Fact]
    public async Task SandboxDispose_GoneContainer_EndsGone()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", "gone")));

        var box = (await BoxesAsync()).Single();
        (box.EndReason, box.ExitCode).Should().Be(("gone", null));
    }

    [Fact]
    public async Task SandboxDispose_ExitedOom_EndsOomKilledWithCode()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", "exited", 137, true)));

        var box = (await BoxesAsync()).Single();
        (box.EndReason, box.ExitCode, box.OomKilled).Should().Be(("oom-killed", 137, true));
    }

    [Fact]
    public async Task SandboxEndFold_WaitingForInput_MarksReleasedAtPark()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", "running")), (4, Finished("waiting_for_input")));

        (await BoxesAsync()).Single().EndReason.Should().Be("released-at-park");
    }

    [Fact]
    public async Task SandboxEndFold_Terminal_MarksReleasedAtEnd()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", exitState: null)), (4, Finished("success")));

        (await BoxesAsync()).Single().EndReason.Should().Be("released-at-end");
    }

    [Fact]
    public async Task SandboxEndFold_QueuedFinished_MarksReleasedAtRequeue_NotAPark()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", "running")), (4, Finished("queued")));

        (await BoxesAsync()).Single().EndReason.Should().Be("released-at-requeue");
    }

    [Fact]
    public async Task SandboxEndFold_NoCloseEvent_MarksUnobserved_KeepsDisposedAtNull()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Finished("failed")));

        var box = (await BoxesAsync()).Single();
        box.EndReason.Should().Be("unobserved");
        box.DisposedAt.Should().BeNull();
    }

    [Fact]
    public async Task SandboxEndFold_ReplayedParkFinished_LeavesLaterLaunchRowsReleased()
    {
        var park = Finished("waiting_for_input");
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", "running")), (4, park),
            (5, Created("api", minute: 10)), (6, Disposed("api", "running", minute: 11)));

        await ApplyAsync((4, park));

        var boxes = await BoxesAsync();
        boxes.Select(b => b.EndReason).Should().Equal("released-at-park", "released");
    }

    [Fact]
    public async Task SandboxEndFold_PositionlessTerminal_FoldsEveryPositionedRow()
    {
        await ApplyAsync((1, Started()), (2, Created("api")), (3, Disposed("api", "running")), (4, Finished("waiting_for_input")),
            (5, Created("api", minute: 10)), (6, Created("web", minute: 10)), (7, Disposed("api", "running", minute: 11)));

        await ApplyAsync((null, Finished("success")));

        var boxes = await BoxesAsync();
        boxes.Select(b => b.EndReason).Should().Equal("released-at-park", "released-at-end", "unobserved");
    }

    [Fact]
    public async Task SandboxDispose_ReplayedAfterRelaunch_LeavesLaterLaunchRowUntouched()
    {
        var firstDispose = Disposed("api", "exited", 1, false);
        var events = new (long?, RunEvent)[]
        {
            (1, Started()), (2, Created("api")), (3, firstDispose), (4, Finished("waiting_for_input")),
            (5, Created("api", minute: 10)), (6, Disposed("api", "running", minute: 30)), (7, Finished("success", minute: 31)),
        };
        await ApplyAsync(events);
        var before = await SnapshotAsync();

        await ApplyAsync(events);
        await ApplyAsync((3, firstDispose));

        var boxes = await BoxesAsync();
        boxes.Should().HaveCount(2);
        boxes.Select(b => (b.EndReason, b.ExitCode)).Should().Equal(("exited", 1), ("released-at-end", null));
        boxes[1].DisposedAt.Should().Be(T.AddMinutes(30));
        (await SnapshotAsync()).Should().Be(before, "a replay with kept positions changes no row and no Gi-minute");
    }

    [Fact]
    public async Task SandboxDispose_NoPositionedRow_ClosesLatestPositionlessRow()
    {
        await using (var db = Context())
        {
            db.Add(new RunSandbox { RunId = RunId, Key = "api", RepoName = "api", SpawnedAt = T });
            await db.SaveChangesAsync();
        }

        await ApplyAsync((40, Disposed("api", "exited", 0, false)));

        var box = (await BoxesAsync()).Single();
        (box.EndReason, box.EventSeq).Should().Be(("exited", null));
        box.DisposedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RunSandbox_ReplayedCreate_AddsNoRow()
    {
        await ApplyAsync((1, Started()), (2, Created("api")));
        await ApplyAsync((2, Created("api")));

        (await BoxesAsync()).Should().ContainSingle().Which.EventSeq.Should().Be(2);
    }

    private static RunStartedEvent Started() => new(RunId, "ticket", "code", ["api", "web"], T, "claude", "42");

    private static SandboxCreatedEvent Created(string repo, int minute = 1) =>
        new(RunId, repo, "node:20", "typescript", T.AddMinutes(minute), MemoryRequest: "1Gi");

    private static SandboxDisposedEvent Disposed(
        string repo, string? exitState, int? exitCode = null, bool? oom = null, int minute = 5) =>
        new(RunId, repo, exitCode, T.AddMinutes(minute), exitState, oom);

    private static SandboxVanishedEvent Vanished(string repo, int? exitCode, bool? oom) =>
        new(RunId, "job-1", repo, T.AddMinutes(2), "sandbox-vanished", "exited", T.AddMinutes(3), exitCode, oom);

    private static RunFinishedEvent Finished(string status, int minute = 6) =>
        new(RunId, status, null, "s", T.AddMinutes(minute));

    private async Task ApplyAsync(params (long? Seq, RunEvent Event)[] events)
    {
        foreach (var (seq, ev) in events)
        {
            await using var db = Context();
            await _applier.ApplyAsync(db, ev, seq, CancellationToken.None);
        }
    }

    private async Task<List<RunSandbox>> BoxesAsync()
    {
        await using var db = Context();
        return await db.RunSandboxes.AsNoTracking().Where(s => s.RunId == RunId).OrderBy(s => s.Id).ToListAsync();
    }

    private async Task<string> SnapshotAsync()
    {
        var rows = string.Join("|", (await BoxesAsync()).Select(b => $"{b.EventSeq}:{b.EndReason}:{b.ExitCode}:{b.DisposedAt:O}"));
        await using var db = Context();
        var run = await new RunRepository(db).GetRunDetailAsync(RunId, CancellationToken.None);
        return $"{rows}#{ReservedCapacityCalculator.Compute(run!):0.###}";
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
