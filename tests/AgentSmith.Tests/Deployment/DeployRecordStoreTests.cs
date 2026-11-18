using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

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

    public void Dispose() => _connection.Dispose();

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

    private static DeployDeliveredEvent Delivered(string? pipelineRunId) => new("r-1", DateTimeOffset.Parse("2026-11-18T10:00:00Z"),
        "shop", "api", null, "test", "v2", "pipeline_trigger", "app", "https://ci.example/runs/42", null, pipelineRunId);
}
