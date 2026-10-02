using AgentSmith.Application.Services.Claim;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-02-75dc: an init run holds no lease, so with Redis flushed only its own row beat
/// can tell the sandbox reapers that it is alive.
/// </summary>
public sealed class LiveRunSetReaderTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly SettableClock _clock = new();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task LiveRunSetReader_AnInitRunBeatingItsRow_IsLiveWithAnEmptyRedisAndNoLease()
    {
        SeedRunningInit("init-1", started: _clock.Now.AddMinutes(-20));
        var heartbeat = NewHeartbeat();
        await heartbeat.RenewAsync("init-1", CancellationToken.None);
        _clock.Now = _clock.Now.AddMinutes(1);

        var reader = new LiveRunSetReader(
            InMemoryRedis.Connection(), new NoOpActiveRunLease(), heartbeat,
            NullLogger<LiveRunSetReader>.Instance);

        (await reader.ReadAsync(CancellationToken.None)).Should().Contain("init-1");
    }

    private DbRunHeartbeat NewHeartbeat()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunLivenessRepository>();
        return new DbRunHeartbeat(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _clock);
    }

    private void SeedRunningInit(string id, DateTimeOffset started)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run { Id = id, Project = "p1", Pipeline = "init-project", Status = "running", StartedAt = started });
        ctx.SaveChanges();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
