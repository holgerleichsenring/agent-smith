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

/// <summary>2026-11-17-3435b: a pull request waiting for CI is still the attempt's open pull request — a rework reads it —
/// and the attempt's own awaiting ones are what a merge-green run waits on.</summary>
public sealed class PreviousAttemptAwaitingCiTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task AwaitingCi_CountsAsOpened_AndFillsAwaitingCiUrls()
    {
        await using (var db = Db())
        {
            db.Add(new Run
            {
                Id = "2026-11-17T09-00-00-a", Project = "p", TicketId = "42", Pipeline = "code", Status = RunStatuses.Success,
                StartedAt = T0, FinishedAt = T0.AddMinutes(1),
                PullRequestsJson = RunStoryJson.Serialize(new List<RunPullRequestView>
                {
                    new("api", PullRequestStatuses.AwaitingCi, "https://x/api/pull/1", null, T0),
                    new("web", PullRequestStatuses.NoChanges, null, null, T0),
                }),
            });
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var attempt = (await new DbPreviousAttemptReader(scopes, new DecidedDefaults(scopes)).LatestAsync("p", "42", null, default))!;

        attempt.OpenedPullRequest.Should().BeTrue();
        attempt.PullRequestUrls.Should().Equal(new Dictionary<string, string> { ["api"] = "https://x/api/pull/1" });
        attempt.AwaitingCiUrls.Should().Equal(new Dictionary<string, string> { ["api"] = "https://x/api/pull/1" });
    }
}
