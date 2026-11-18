using AgentSmith.Contracts.Runs;
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

/// <summary>2026-11-17-3435c: the newest attempt's cause, and how many newest attempts in a row a red CI started.</summary>
public sealed class PreviousAttemptCiStreakTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task CiFixStreak_CountsConsecutiveCiCauses_FromTheNewest()
    {
        await using (var db = Db())
        {
            string?[] causes = [AttemptChannels.Pickup, AttemptChannels.Ci, AttemptChannels.TicketComment, AttemptChannels.Ci, AttemptChannels.Ci];
            for (var i = 0; i < causes.Length; i++)
            {
                var id = $"2026-11-17T09-0{i}-00-a";
                db.Add(new Run { Id = id, Project = "p", TicketId = "42", Pipeline = "code", Status = RunStatuses.Success,
                    StartedAt = T0.AddMinutes(i), FinishedAt = T0.AddMinutes(i + 1) });
                db.Add(new RunAttempt { RunId = id, CauseChannel = causes[i] });
            }
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var attempt = (await new DbPreviousAttemptReader(scopes, new DecidedDefaults(scopes)).LatestAsync("p", "42", null, default))!;

        attempt.CauseChannel.Should().Be(AttemptChannels.Ci);
        attempt.CiFixStreak.Should().Be(2);
        attempt.FinishedAt.Should().Be(T0.AddMinutes(5));
    }
}
