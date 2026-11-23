using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Repair;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Deployment;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863f: a deploy's delivery becomes its record over a migrated store — once; a replayed event leaves
/// what the follow read since alone — and the run's endpoint answers it.</summary>
public sealed class DeployRecordStoreTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    [Fact]
    public async Task Apply_InsertsOnce_AReplayKeepsTheFollowedState()
    {
        await ApplyAsync(Delivered("42"));
        await using (var ctx = MigratedStoreTemplate.Context(_connection))
        {
            var record = (await new DeployRecordRepository(ctx).UnsettledAsync(10, default)).Single();
            (record.CiState, record.SettledAtTicks) = (CiStates.Green, 1);
            await ctx.SaveChangesAsync();
        }

        await ApplyAsync(Delivered("42"));

        var view = await ByRunAsync("r-1");
        view!.CiState.Should().Be(CiStates.Green, "a replay must not reset what the follow read");
        view.Delivery.Should().Be("pipeline_trigger");
    }

    [Fact]
    public async Task Apply_PipelineRunTheHostDidNotName_IsInsertedSettled() 
    {
        await ApplyAsync(Delivered(null));

        (await ByRunAsync("r-1"))!.Note.Should().Contain("not followed");
    }

    [Fact]
    public async Task Endpoint_AnswersTheRunsRecord_404WithoutOne()
    {
        await ApplyAsync(Delivered("42"));
        await using var ctx = MigratedStoreTemplate.Context(_connection);
        var records = new DeployRecordRepository(ctx);

        (await DeploymentEndpoints.RecordAsync("r-1", records, default)).Should().BeOfType<Ok<AgentSmith.Contracts.Deployment.DeployRecordView>>();
        (await DeploymentEndpoints.RecordAsync("r-2", records, default)).Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Apply_RecordCarriesTheEventsTimestampTicks()
    {
        // 2026-11-23-af60: 3863h commented the assignment out, so every record was stored delivered at tick 0.
        await ApplyAsync(Delivered("42"));

        (await RecordAsync("r-1")).DeliveredAtTicks.Should().Be(At.UtcTicks);
        (await ByRunAsync("r-1"))!.DeliveredAt.Should().Be(At);
    }

    [Fact]
    public async Task Verdict_AFreshProjectedRecordReadPending_IsFollowed_UntilCiPendingSecondsPass()
    {
        // 2026-11-23-af60: with DeliveredAtTicks 0 the first pending read settled it as timed out.
        var merge = new MergePolicyConfig { CiPollSeconds = 60, CiPendingSeconds = 3600 };
        var tickets = new TicketsConfig { PrRereadIntervalSeconds = 900 };
        await ApplyAsync(Delivered("42"));
        var record = await RecordAsync("r-1");
        record.CheckedAtTicks = At.AddMinutes(5).UtcTicks;

        DeployFollowVerdict.Of(record, merge, At.AddMinutes(5)).Should().Be((false, (string?)null));
        DeployFollowVerdict.IsDue(record, merge, tickets, At.AddMinutes(6)).Should().BeTrue("a pending run is read every ci_poll_seconds");
        DeployFollowVerdict.Of(record, merge, At.AddHours(2)).Settled.Should().BeTrue();
    }

    [Fact]
    public async Task Backfill_FillsZeroRowsFromTheirDeliveredEvent_LeavesTheRest_ASecondPassChangesNothing()
    {
        await using (var db = MigratedStoreTemplate.Context(_connection))
        {
            db.RunEvents.Add(RunTrailRowMapper.Map("r-1", 1, Delivered("42")));
            db.RunEvents.Add(RunTrailRowMapper.Map("r-2", 1, Delivered("43") with { RunId = "r-2" }));
            db.AddRange(Row("r-1", 0), Row("r-2", 7), Row("r-3", 0));
            await db.SaveChangesAsync();
        }
        var backfill = new DeployDeliveredAtBackfill(NullLogger<DeployDeliveredAtBackfill>.Instance);

        int first, second;
        await using (var db = MigratedStoreTemplate.Context(_connection)) first = await backfill.RepairAsync(db, default);
        await using (var db = MigratedStoreTemplate.Context(_connection)) second = await backfill.RepairAsync(db, default);

        (first, second).Should().Be((1, 0));
        (await RecordAsync("r-1")).DeliveredAtTicks.Should().Be(At.UtcTicks);
        (await RecordAsync("r-2")).DeliveredAtTicks.Should().Be(7, "a filled row is left as it is");
        (await RecordAsync("r-3")).DeliveredAtTicks.Should().Be(0, "a row whose event is gone has nothing to read");
    }

    // 2026-11-23-8df6a: the Deployments page's read — the scope's records delivered inside the window, newest first.
    [Fact]
    public async Task DeliveredSinceAsync_Scoped_ReturnsAdmittedOnly()
    {
        await using (var db = MigratedStoreTemplate.Context(_connection))
        {
            var other = Row("r-other", At.UtcTicks);
            other.Project = "other";
            db.AddRange(Row("r-1", At.UtcTicks), Row("r-2", At.AddHours(1).UtcTicks), Row("r-old", At.AddDays(-10).UtcTicks), other);
            await db.SaveChangesAsync();
        }
        await using var ctx = MigratedStoreTemplate.Context(_connection);

        var listed = await new DeployRecordRepository(ctx).DeliveredSinceAsync(
            AgentSmith.Infrastructure.Persistence.Contracts.ProjectScope.Single("shop"), At.AddDays(-1), default);

        listed.Select(r => r.RunId).Should().Equal("r-2", "r-1");
    }

    public void Dispose() => _connection.Dispose();

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-18T10:00:00Z");

    private static DeployRecord Row(string runId, long deliveredAt) => new()
    {
        RunId = runId, Project = "shop", Context = "api", Environment = "test", Version = "v2", Delivery = "pipeline_trigger",
        Repo = "app", Url = "https://ci.example/runs/42", PipelineRunId = "42", CiState = CiStates.Pending, DeliveredAtTicks = deliveredAt,
    };

    private async Task<DeployRecord> RecordAsync(string runId)
    {
        await using var ctx = MigratedStoreTemplate.Context(_connection);
        return await ctx.Set<DeployRecord>().AsNoTracking().SingleAsync(r => r.RunId == runId);
    }

    private async Task ApplyAsync(DeployDeliveredEvent e)
    {
        await using var ctx = MigratedStoreTemplate.Context(_connection);
        await new DeployRecordProjection().ApplyAsync(ctx, e, default);
    }

    private async Task<AgentSmith.Contracts.Deployment.DeployRecordView?> ByRunAsync(string runId)
    {
        await using var ctx = MigratedStoreTemplate.Context(_connection);
        return await new DeployRecordRepository(ctx).ByRunAsync(runId, default);
    }

    private static DeployDeliveredEvent Delivered(string? pipelineRunId) => new("r-1", At,
        "shop", "api", null, "test", "v2", "pipeline_trigger", "app", "https://ci.example/runs/42", null, pipelineRunId);
}
