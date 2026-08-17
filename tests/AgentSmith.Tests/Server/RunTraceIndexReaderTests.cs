using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0423b: a traced run's conversation is readable entry by entry, in call order. The list
/// carries sizes and never content — a recorded prompt reaches megabytes.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunTraceIndexReaderTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RunTraceIndexReaderTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task TraceReader_ShowsEntriesInCallOrder()
    {
        var reader = NewReader();
        await WriteAsync("run-1", RecordedTraceKey.Format(2, "answer"), "ok");
        await WriteAsync("run-1", RecordedTraceKey.Format(1, "prompt"), "a long prompt");
        await WriteAsync("run-1", RecordedTraceKey.Format(10, "tool"), "tool output");

        var entries = await reader.ListAsync("run-1", CancellationToken.None);

        entries.Select(e => e.Sequence).Should().Equal(1, 2, 10);
        entries.Select(e => e.Label).Should().Equal("prompt", "answer", "tool");
        entries[0].Chars.Should().Be("a long prompt".Length, "the list carries sizes, never content");
    }

    [Fact]
    public async Task AnUntracedRun_HasNoEntries_AndIsNotAnError()
    {
        var entries = await NewReader().ListAsync("never-traced", CancellationToken.None);

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task OneEntry_ReadsBackTheContentItWasRecordedWith()
    {
        var reader = NewReader();
        await WriteAsync("run-1", RecordedTraceKey.Format(7, "prompt"), "what the model saw");

        var content = await reader.ReadAsync("run-1", 7, "prompt", CancellationToken.None);
        var missing = await reader.ReadAsync("run-1", 8, "prompt", CancellationToken.None);

        content.Should().Be("what the model saw");
        missing.Should().BeNull();
    }

    private RunTraceIndexReader NewReader() => new(ScopeFactory());

    private async Task WriteAsync(string runId, string kind, string content)
    {
        using var scope = ScopeFactory().CreateScope();
        await scope.ServiceProvider.GetRequiredService<RunArtifactRepository>()
            .UpsertAsync(runId, kind, content, CancellationToken.None);
    }

    private IServiceScopeFactory ScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options));
        services.AddScoped<RunArtifactRepository>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _connection.Dispose();
}
