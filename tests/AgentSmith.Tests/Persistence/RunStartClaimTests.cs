using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-02-5ab2b: the claim a consumer makes at the pop, over real rows. Of two copies of one
/// stored request exactly one starts; a run that already runs drops its copy; a row that keeps
/// no request (a ticket run) starts as it always did.
/// </summary>
public sealed class RunStartClaimTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _services;

    public RunStartClaimTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => MigratedStoreTemplate.Context(_connection));
        services.AddScoped<QueuedRunRepository>();
        _services = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunStartClaim_TwoCopiesOfOneRequest_StartOnce()
    {
        await SeedAsync("run-1", "queued", request: "{}");

        var first = await Claim().ClaimAsync("run-1", CancellationToken.None);
        var second = await Claim().ClaimAsync("run-1", CancellationToken.None);

        first.Should().Be(RunStartClaimOutcome.Claimed);
        second.Should().Be(RunStartClaimOutcome.Duplicate, "the copy lost the conditional update");
        var row = Row("run-1");
        row.ClaimedAt.Should().NotBeNull();
        row.HeartbeatAt.Should().Be(row.ClaimedAt, "the claim is the row's first beat");
    }

    [Fact]
    public async Task RunStartClaim_RowAlreadyRunning_DropsTheCopy()
    {
        await SeedAsync("run-1", "running", request: null);

        (await Claim().ClaimAsync("run-1", CancellationToken.None)).Should().Be(RunStartClaimOutcome.Duplicate);
    }

    [Fact]
    public async Task RunStartClaim_RowWithoutStoredRequest_StartsAsToday()
    {
        await SeedAsync("run-1", "queued", request: null);

        (await Claim().ClaimAsync("run-1", CancellationToken.None)).Should().Be(RunStartClaimOutcome.Unclaimed);
        (await Claim().ClaimAsync("no-row", CancellationToken.None)).Should().Be(RunStartClaimOutcome.Unclaimed);
        Row("run-1").ClaimedAt.Should().BeNull();
    }

    [Fact]
    public async Task QueuedRunRepository_StoreRequest_ResetsAnEarlierClaim()
    {
        await SeedAsync("run-1", "waiting_for_input", request: "{\"old\":1}");
        await Claim().ClaimAsync("run-1", CancellationToken.None);
        var at = DateTimeOffset.UtcNow;

        using (var scope = _services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<QueuedRunRepository>()
                .StoreRequestAsync("run-1", "{\"new\":1}", at, CancellationToken.None);

        var row = Row("run-1");
        row.QueuedRequestJson.Should().Be("{\"new\":1}");
        row.RequestEnqueuedAt.Should().Be(at);
        row.ClaimedAt.Should().BeNull("a new request is judged under a new claim");
    }

    [Fact]
    public async Task QueuedRunProjection_Promotion_ClearsRequestAndClaim()
    {
        await SeedAsync("run-1", "queued", request: "{}");
        await Claim().ClaimAsync("run-1", CancellationToken.None);

        await using (var db = MigratedStoreTemplate.Context(_connection))
        {
            var run = await db.Runs.SingleAsync(r => r.Id == "run-1");
            await new QueuedRunProjection().PromoteToRunningAsync(db, run,
                new RunStartedEvent("run-1", "manual", "init-project", ["repo"], DateTimeOffset.UtcNow, "claude", null),
                CancellationToken.None);
        }

        var promoted = Row("run-1");
        promoted.Status.Should().Be("running");
        promoted.QueuedRequestJson.Should().BeNull();
        promoted.RequestEnqueuedAt.Should().BeNull();
        promoted.ClaimedAt.Should().BeNull();
    }

    private DbRunStartClaim Claim() => new(_services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);

    private async Task SeedAsync(string id, string status, string? request)
    {
        await using var db = MigratedStoreTemplate.Context(_connection);
        db.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project", Status = status, StartedAt = DateTimeOffset.UtcNow,
            QueuedRequestJson = request, RequestEnqueuedAt = request is null ? null : DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private Run Row(string id)
    {
        using var db = MigratedStoreTemplate.Context(_connection);
        return db.Runs.AsNoTracking().Single(r => r.Id == id);
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
