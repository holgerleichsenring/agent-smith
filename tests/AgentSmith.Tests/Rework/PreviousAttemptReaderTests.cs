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
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new DbPreviousAttemptReader(scopes, new DecidedDefaults(scopes));
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

    // 2026-10-13-8762p: the overruled defaults of every code run of the ticket, oldest decision first; pending,
    // ratified, scan and other-ticket rows are not overrules.
    [Fact]
    public async Task PreviousAttempt_CarriesOverruledDefaultsOfTheTicket()
    {
        await SeedAsync(
            ("2026-10-08T09-00-00-a", "code", "success", 0, true),
            ("2026-10-08T09-10-00-b", "security-scan", "success", 10, true),
            ("2026-10-08T09-20-00-c", "code", "success", 20, true));
        await using (var db = Db())
        {
            db.AddRange(
                Row(1, "2026-10-08T09-00-00-a", "overruled", 50), Row(2, "2026-10-08T09-20-00-c", "overruled", 40),
                Row(3, "2026-10-08T09-20-00-c", "ratified", 45), Row(4, "2026-10-08T09-20-00-c", "pending", null),
                Row(5, "2026-10-08T09-10-00-b", "overruled", 30), Row(6, "elsewhere", "overruled", 30));
            await db.SaveChangesAsync();
        }

        var attempt = await Reader().LatestAsync("p", "42", null, CancellationToken.None);

        attempt!.Overrules.Select(o => (o.Id, o.By, o.At, o.Note)).Should().Equal(
            (2L, "ana", T0.AddMinutes(40), "no"), (1L, "ana", T0.AddMinutes(50), "no"));
    }

    private static RunDefaultDecision Row(long id, string runId, string verdict, int? minutes) => new()
    {
        Id = id, RunId = runId, Question = "q", Chosen = "c", Kind = "decision", TakenAt = T0, Verdict = verdict,
        VerdictBy = minutes is null ? null : "ana", VerdictAt = minutes is { } m ? T0.AddMinutes(m) : null,
        VerdictNote = minutes is null ? null : "no",
    };
}
