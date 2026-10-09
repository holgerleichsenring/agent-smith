using AgentSmith.Tests.TestSupport;
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
/// p0246e: the markdown slots (result.md / plan.md / analyze.md) live in the DB, so they
/// survive a Redis flush AND a process restart. Proven on a real SQLite engine.
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
        MigratedStoreTemplate.CopyInto(ctx);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    // The store opens a scope per op; its scoped IUnitOfWork is a fresh
    // context over the same in-memory connection.
    private DbRunArtifactStore NewStore()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunArtifactRepository>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new DbRunArtifactStore(scopeFactory);
    }

    [Fact]
    public async Task DbRunArtifactStore_ResultMarkdown_IsReadFromTheDatabaseWithoutRedis()
    {
        await NewStore().WriteResultMarkdownAsync("run-1", "# Result", CancellationToken.None);

        // A second store instance shares nothing but the database.
        var result = await NewStore().ReadResultMarkdownAsync("run-1", CancellationToken.None);

        result.Should().Be("# Result", "the database is the only copy (2026-10-02-5ab2f)");
    }

    [Fact]
    public async Task PlanMarkdown_PersistsToDb_AndReadsBack()
    {
        var store = NewStore();
        await store.WritePlanMarkdownAsync("run-1", "# Plan", CancellationToken.None);

        using var ctx = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        ctx.RunArtifacts.Should().ContainSingle(a => a.RunId == "run-1" && a.Kind == "plan_md" && a.Content == "# Plan");
    }

    [Fact]
    public async Task UnwrittenSlot_ReadsNull()
    {
        (await NewStore().ReadAnalyzeMarkdownAsync("run-1", CancellationToken.None)).Should().BeNull();
    }

    private sealed class Factory(SqliteConnection connection) : IDbContextFactory<AgentSmithDbContext>
    {
        public AgentSmithDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);
    }

    public void Dispose() => _connection.Dispose();
}
