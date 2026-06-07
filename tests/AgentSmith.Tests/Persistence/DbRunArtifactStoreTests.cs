using AgentSmith.Application.Services.Persistence;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246e: the durable markdown slots (result.md / plan.md / analyze.md) are
/// mirrored to the DB, so they survive a Redis flush AND a process restart. The
/// transient slots stay with the inner store. Proven on a real SQLite engine,
/// with an in-memory inner store standing in for Redis.
/// </summary>
public sealed class DbRunArtifactStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbRunArtifactStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        ctx.Database.Migrate();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    // The decorator opens a scope per op; its scoped IUnitOfWork is a fresh
    // context over the same in-memory connection.
    private DbRunArtifactStore NewStore(InMemoryRunArtifactStore inner)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunArtifactRepository>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new DbRunArtifactStore(inner, scopeFactory);
    }

    [Fact]
    public async Task ResultMarkdown_SurvivesRedisFlush_ReadsFromDb()
    {
        var redis = new InMemoryRunArtifactStore();
        await NewStore(redis).WriteResultMarkdownAsync("run-1", "# Result", CancellationToken.None);

        // Simulate a Redis flush: a brand-new inner store has nothing. The DB copy
        // must still serve the result.
        var afterFlush = NewStore(new InMemoryRunArtifactStore());
        var result = await afterFlush.ReadResultMarkdownAsync("run-1", CancellationToken.None);

        result.Should().Be("# Result", "the DB mirror survives a Redis flush");
    }

    [Fact]
    public async Task PlanMarkdown_PersistsToDb_AndReadsBack()
    {
        var store = NewStore(new InMemoryRunArtifactStore());
        await store.WritePlanMarkdownAsync("run-1", "# Plan", CancellationToken.None);

        using var ctx = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        ctx.RunArtifacts.Should().ContainSingle(a => a.RunId == "run-1" && a.Kind == "plan_md" && a.Content == "# Plan");
    }

    [Fact]
    public async Task TransientSlots_StayWithInnerStore_NotMirroredToDb()
    {
        var redis = new InMemoryRunArtifactStore();
        var store = NewStore(redis);

        await store.WritePlanAsync("run-1", "{\"plan\":1}", CancellationToken.None);

        (await redis.ReadPlanAsync("run-1", CancellationToken.None)).Should().Be("{\"plan\":1}");
        using var ctx = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        ctx.RunArtifacts.Should().BeEmpty("the transient plan slot is not run history — it stays in the inner store");
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<AgentSmithDbContext>
    {
        public AgentSmithDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);
    }

    public void Dispose() => _connection.Dispose();
}
