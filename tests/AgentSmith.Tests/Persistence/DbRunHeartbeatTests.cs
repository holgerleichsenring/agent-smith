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

/// <summary>2026-10-02-5f89e: the run row's beat is one column, written for a live row only.</summary>
public sealed class DbRunHeartbeatTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly SettableClock _clock = new();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RenewAsync_LiveRow_StampsHeartbeatAt()
    {
        Seed("live", finished: null);

        await NewHeartbeat().RenewAsync("live", CancellationToken.None);

        Row("live").HeartbeatAt.Should().Be(_clock.Now);
    }

    [Fact]
    public async Task RenewAsync_FinishedRow_KeepsItsLastBeat()
    {
        Seed("done", finished: _clock.Now.AddMinutes(-1));

        await NewHeartbeat().RenewAsync("done", CancellationToken.None);

        Row("done").HeartbeatAt.Should().BeNull();
    }

    private DbRunHeartbeat NewHeartbeat()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunLivenessRepository>();
        return new DbRunHeartbeat(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _clock);
    }

    private void Seed(string id, DateTimeOffset? finished)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project", Status = finished is null ? "running" : "success",
            StartedAt = _clock.Now.AddMinutes(-10), FinishedAt = finished,
        });
        ctx.SaveChanges();
    }

    private Run Row(string id)
    {
        using var ctx = new AgentSmithDbContext(Options());
        return ctx.Runs.AsNoTracking().Single(r => r.Id == id);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
