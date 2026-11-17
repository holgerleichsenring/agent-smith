using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Tickets.ShownOnTicket;

/// <summary>
/// 2026-11-17-b34ed: a refresh-base or retarget run is shown on its ticket — its row carries the ticket's id — and
/// does nothing else to the ticket: no RunEnd nudge, no capacity entry, no lease.
/// </summary>
public sealed class ShownOnTicketRunTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    [Theory]
    [InlineData(PipelinePresets.RefreshBaseName)]
    [InlineData(PipelinePresets.RetargetName)]
    public void Presets_ShownOnTicketRuns_LeaveTheTicketAlone(string pipeline)
    {
        PipelinePresets.ShownOnTicketOnly(pipeline).Should().BeTrue();
        PipelinePresets.LeavesTicketAlone(pipeline).Should().BeTrue();
        PipelinePresets.ShownOnTicketOnly(PipelinePresets.CodeName).Should().BeFalse();
    }

    [Fact]
    public void Presets_ARefresh_StillWaitsForCapacity_ADerivationDoesNot()
    {
        PipelinePresets.WaitsForCapacity(PipelinePresets.RefreshBaseName).Should().BeTrue();
        PipelinePresets.WaitsForCapacity(PipelinePresets.SpecDeriveName).Should().BeFalse();
        PipelinePresets.WaitsForCapacity(PipelinePresets.CodeName).Should().BeTrue();
    }

    [Theory]
    [InlineData(PipelinePresets.RefreshBaseName, 0)]
    [InlineData(PipelinePresets.CodeName, 1)]
    public async Task Finish_AShownOnTicketRun_NudgesNoRework(string pipeline, int nudges)
    {
        await FinishAsync(pipeline, "success");

        await using var db = Db();
        (await db.Set<ReworkNudge>().CountAsync()).Should().Be(nudges);
    }

    [Fact]
    public async Task Finish_AQueuedRefresh_WritesNoCapacityEntryForTheTicket()
    {
        await FinishAsync(PipelinePresets.RefreshBaseName, "queued");

        await using var db = Db();
        (await db.Set<QueuedTicket>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Start_AQueuedRefreshRow_TakesTheTicketItIsShownOn()
    {
        await using (var db = Db())
        {
            db.Add(new Run { Id = "r-1", Project = "p", TicketId = "", Pipeline = PipelinePresets.RefreshBaseName,
                Status = "queued", StartedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using (var uow = Db())
        {
            var row = await uow.Set<Run>().SingleAsync();
            await new QueuedRunProjection().PromoteToRunningAsync(uow, row, new RunStartedEvent(
                "r-1", "manual", PipelinePresets.RefreshBaseName, ["api"], DateTimeOffset.UtcNow, "stub", "42",
                Project: "p", Platform: "github"), CancellationToken.None);
        }

        await using var read = Db();
        (await read.Set<Run>().SingleAsync()).TicketId.Should().Be("42");
    }

    private async Task FinishAsync(string pipeline, string status)
    {
        await using (var db = Db())
        {
            db.Add(new Run { Id = "r-1", Project = "p", TicketId = "42", Platform = "github", Pipeline = pipeline,
                Status = "running", StartedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var uow = Db();
        await new RunFinalizationProjection(new QueuedRunProjection(), null, null, new SqliteUniqueViolationTranslator())
            .ApplyAsync(uow, new RunFinishedEvent("r-1", status, null, "done", DateTimeOffset.UtcNow), CancellationToken.None);
    }
}

/// <summary>2026-11-17-b34ed: the ticket page lists a refresh run among the other runs, never as an attempt.</summary>
public sealed class ShownOnTicketDetailTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Detail_ARefreshRunOfTheTicket_IsListedAmongTheOtherRuns()
    {
        await _h.AddAsync(TicketStoreHarness.Run("r-1", "42"), TicketStoreHarness.Step("r-1"),
            TicketStoreHarness.Run("r-2", "42", pipeline: PipelinePresets.RefreshBaseName), TicketStoreHarness.Step("r-2"));

        var detail = await _h.Detail.ReadAsync("alpha", "42", CancellationToken.None);

        detail!.Attempts.Select(a => a.RunId).Should().Equal("r-1");
        detail.OtherRuns.Should().ContainSingle(o => o.RunId == "r-2" && o.Pipeline == PipelinePresets.RefreshBaseName);
    }
}
