using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0369: the metrics fold is REAL data end to end — the applier folds
/// LlmCallFinished + SandboxResult onto the run row (RunMetricsJson) as they
/// arrive, and the run DETAIL serves the top-N projection. The run LIST stays
/// lean (no metrics), and pre-p0369 rows serve an honest null.
/// </summary>
public sealed class RunMetricsServedTests : IDisposable
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
    private readonly SqliteConnection _connection;

    public RunMetricsServedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    [Fact]
    public async Task RunDetail_FoldedMetrics_Served()
    {
        const string runId = "2026-07-22T10-00-00-0001";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["primary"], T, "claude", "42"),
            new LlmCallFinishedEvent(runId, "m", "coder", 2000, 10, 0.02m, 1500, T, ThrottleWaitMs: 400),
            new SandboxResultEvent(runId, "primary", "ReadFile", 0, 50, T, Summary: "src/A.cs", ContentHash: "h1"),
            new SandboxResultEvent(runId, "primary", "ReadFile", 0, 40, T, Summary: "src/A.cs", ContentHash: "h1"),
            new SandboxResultEvent(runId, "primary", "/bin/sh", 1, 9000, T, Summary: "-c dotnet test tests/X.csproj"),
            new RunFinishedEvent(runId, "failed", null, "broke", T.AddMinutes(5)));

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(runId, CancellationToken.None);
        var detail = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);
        var list = RunSnapshotMapper.ToSnapshot(run!); // list path

        detail.Metrics.Should().NotBeNull();
        detail.Metrics!.LlmActiveMs.Should().Be(1500);
        detail.Metrics.ThrottleWaitMs.Should().Be(400);
        detail.Metrics.SandboxCommandMs.Should().Be(9090);
        detail.Metrics.Reads.Should().Be(2);
        detail.Metrics.RedundantReads.Should().Be(1);
        detail.Metrics.ColdLlmCalls.Should().Be(1);
        detail.Metrics.BuildTestInvocations.Should().Be(1);
        detail.Metrics.BuildTestFailures.Should().Be(1);
        detail.Metrics.TopReadFiles.Should().ContainSingle()
            .Which.Should().Be(new AgentSmith.Contracts.Runs.FileAccessView("src/A.cs", 2));

        list.Metrics.Should().BeNull("the list stays lean");
    }

    [Fact]
    public async Task RunDetail_PreMigrationRow_MetricsNull()
    {
        const string runId = "2026-07-22T10-00-00-0002";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["primary"], T),
            new RunFinishedEvent(runId, "success", null, "done", T.AddMinutes(1)));

        var run = await new RunRepository(new AgentSmithDbContext(Options()))
            .GetRunDetailAsync(runId, CancellationToken.None);
        var detail = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);

        detail.Metrics.Should().BeNull("a run with no folded events serves no metrics, not zeros");
    }

    private async Task ApplyAsync(params RunEvent[] events)
    {
        var applier = new RunEventApplier();
        foreach (var ev in events)
        {
            await using var uow = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(uow, ev, CancellationToken.None);
        }
    }
}
