using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
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

/// <summary>2026-10-13-8762g: the boundary is read from the run rows on a real SQLite engine — a finished run whose
/// end the shared rule holds, with a spec it never started.</summary>
public sealed class PreviousAttemptBoundaryTests : IDisposable
{
    private const string RunId = "2026-10-14T09-00-00-a";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task PreviousAttempt_SuccessRunWithNotStartedRow_IsStoppedAtBoundary()
    {
        await SeedAsync("success", null, RunSeriesCutProjection.NotStarted);

        (await Reader().LatestAsync("p", "42", null, CancellationToken.None))!.StoppedAtBoundary.Should().BeTrue();
    }

    [Fact]
    public async Task PreviousAttempt_ShortfallWithNotStartedRow_IsNotStoppedAtBoundary()
    {
        await SeedAsync("shortfall", null, RunSeriesCutProjection.NotStarted);

        (await Reader().LatestAsync("p", "42", null, CancellationToken.None))!.StoppedAtBoundary.Should().BeFalse(
            "a shortfall delivered what it verified; a status move back of it is a person's act");
    }

    [Fact]
    public async Task PreviousAttempt_SuccessRunWithEverySpecStarted_IsNotStoppedAtBoundary()
    {
        await SeedAsync("success", null, "done");

        (await Reader().LatestAsync("p", "42", null, CancellationToken.None))!.StoppedAtBoundary.Should().BeFalse();
    }

    [Fact]
    public async Task Poll_TicketWallTimeCancelledWithTail_IsNotReclaimed()
    {
        await SeedAsync("cancelled", "watchdog-wall-time", RunSeriesCutProjection.NotStarted);
        var config = new AgentSmithConfig
        {
            Projects = new()
            {
                ["p"] = new ResolvedProject { Tracker = new TrackerConnection { Name = "t" }, GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code" } },
            },
        };

        var refusal = await new ClaimRefusal(new InMemoryUnmovedTicketStore(), Reader(), AgentSmith.Tests.TestSupport.TicketRequirementDoubles.None())
            .ForAsync(new ClaimRequest("GitHub", "p", new TicketId("42"), "code"), config, CancellationToken.None);

        refusal!.Rejection.Should().Be(ClaimRejectionReason.TicketStoppedAtBoundary);
    }

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private DbPreviousAttemptReader Reader()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new DbPreviousAttemptReader(scopes, new DecidedDefaults(scopes));
    }

    private async Task SeedAsync(string status, string? cancelReason, string specStatus)
    {
        await using var db = Db();
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        db.Add(new Run
        {
            Id = RunId, Project = "p", TicketId = "42", Pipeline = "code", Status = status, CancelReason = cancelReason,
            StartedAt = at, FinishedAt = at.AddMinutes(30),
        });
        db.Add(new RunSpec { RunId = RunId, SpecId = "2026-10-14-1234a", Ordinal = 1, Title = "a", Status = "done" });
        db.Add(new RunSpec { RunId = RunId, SpecId = "2026-10-14-1234b", Ordinal = 2, Title = "b", Status = specStatus });
        await db.SaveChangesAsync();
    }
}
