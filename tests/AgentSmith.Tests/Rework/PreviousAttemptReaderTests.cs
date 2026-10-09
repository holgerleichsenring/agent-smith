using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-7c0e: the previous attempt is the newest code-pipeline Run row of the
/// ticket, this run and queued reservations left out — on a real SQLite engine.</summary>
public sealed class PreviousAttemptReaderTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private static readonly DateTimeOffset T0 = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private DbPreviousAttemptReader Reader()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        return new DbPreviousAttemptReader(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private async Task SeedAsync(params (string Id, string Pipeline, string Status, int Minutes, bool Finished)[] runs)
    {
        await using var db = Db();
        foreach (var r in runs)
            db.Add(new Run
            {
                Id = r.Id, Project = "p", TicketId = "42", Pipeline = r.Pipeline, Status = r.Status,
                StartedAt = T0.AddMinutes(r.Minutes), FinishedAt = r.Finished ? T0.AddMinutes(r.Minutes + 5) : null,
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PreviousAttempt_ExcludesThisRunQueuedRowsAndScans()
    {
        await SeedAsync(
            ("2026-10-08T09-00-00-a", "code", "success", 0, true),
            ("2026-10-08T09-10-00-b", "security-scan", "success", 10, true),
            ("2026-10-08T09-20-00-c", "code", "queued", 20, false),
            ("2026-10-08T09-30-00-d", "code", "running", 30, false));

        var attempt = await Reader().LatestAsync("p", "42", "2026-10-08T09-30-00-d", CancellationToken.None);

        attempt!.RunId.Should().Be("2026-10-08T09-00-00-a");
        attempt.Finished.Should().BeTrue();
        attempt.StartedAt.Should().Be(T0);
    }

    [Fact]
    public async Task PreviousAttempt_NoExclusion_ReturnsNewestIncludingRunning()
    {
        await SeedAsync(("2026-10-08T09-00-00-a", "code", "failed", 0, true), ("2026-10-08T09-30-00-d", "code", "running", 30, false));

        var attempt = await Reader().LatestAsync("p", "42", null, CancellationToken.None);

        attempt!.RunId.Should().Be("2026-10-08T09-30-00-d");
        attempt.Finished.Should().BeFalse();
    }

    [Fact]
    public async Task PreviousAttempt_OtherProjectOrTicket_ReturnsNull()
    {
        await SeedAsync(("2026-10-08T09-00-00-a", "code", "success", 0, true));

        (await Reader().LatestAsync("other", "42", null, CancellationToken.None)).Should().BeNull();
        (await Reader().LatestAsync("p", "7", null, CancellationToken.None)).Should().BeNull();
    }
}
