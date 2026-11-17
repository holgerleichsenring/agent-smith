using AgentSmith.Contracts.Runs;
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

namespace AgentSmith.Tests.Preconditions;

/// <summary>2026-11-16-f3fbc: the stuck tickets are those whose newest code attempt — queued rows ignored — failed on
/// a precondition kind, read on a real SQLite engine with the detail the refusal stored.</summary>
public sealed class StuckOnPreconditionReaderTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static Run Row(string id, string ticket, string status, string? kind = null, string pipeline = "code") => new()
    {
        Id = id, Project = "p", TicketId = ticket, Pipeline = pipeline, Status = status, StartedAt = T0,
        FinishedAt = status is RunStatuses.Queued or RunStatuses.Running ? null : T0.AddMinutes(1), EndKind = kind,
        EndDetailJson = kind is null ? null : EndDetailJson.Write(new EndDetail($"subject {ticket}", ["api"])),
    };

    private async Task<IReadOnlyList<StuckOnPrecondition>> ListAsync(params Run[] rows)
    {
        await using (var db = Db())
        {
            db.AddRange(rows);
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        return await new DbStuckOnPreconditionReader(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>())
            .ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task List_NewestAttemptFailedOnAPrecondition_Listed()
    {
        var stuck = await ListAsync(
            Row("2026-11-16T09-00-00-a", "1", RunStatuses.Failed, RunEndKinds.ImageUntrusted),
            Row("2026-11-16T09-00-00-b", "2", RunStatuses.Failed, RunEndKinds.BootstrapMissing),
            Row("2026-11-16T09-00-01-b", "2", RunStatuses.Queued),
            Row("2026-11-16T09-00-00-c", "3", RunStatuses.Failed, RunEndKinds.ReadinessUnknown),
            Row("2026-11-16T09-00-00-d", "4", RunStatuses.Failed, RunEndKinds.Preflight),
            Row("2026-11-16T09-00-01-d", "4", RunStatuses.Running),
            Row("2026-11-16T09-00-00-e", "5", RunStatuses.Failed, RunEndKinds.Preflight, pipeline: "spec-derive"));

        stuck.Select(s => (s.TicketId, s.EndKind)).Should().Equal(("1", RunEndKinds.ImageUntrusted), ("2", RunEndKinds.BootstrapMissing));
        stuck[0].Should().Match<StuckOnPrecondition>(s => s.RunId == "2026-11-16T09-00-00-a" && s.Detail!.Subject == "subject 1"
            && s.FinishedAt == T0.AddMinutes(1));
    }

    [Fact]
    public async Task List_ClosedOrLandedTicket_Left()
    {
        await using (var db = Db())
        {
            db.Add(new ClosedTicket { Tracker = "t", TicketKey = "github-1", TicketId = "1", Project = "p", ClosedAt = T0, By = "a" });
            db.Add(new LandedTicket { Tracker = "t", TicketKey = "github-2", TicketId = "2", Project = "p", Kind = "pr", LandedAt = T0 });
            db.Add(new ClosedTicket { Tracker = "t", TicketKey = "github-3", TicketId = "3", Project = "other", ClosedAt = T0, By = "a" });
            await db.SaveChangesAsync();
        }

        var stuck = await ListAsync(
            Row("2026-11-16T09-00-00-a", "1", RunStatuses.Failed, RunEndKinds.ImageUntrusted),
            Row("2026-11-16T09-00-00-b", "2", RunStatuses.Failed, RunEndKinds.BootstrapMissing),
            Row("2026-11-16T09-00-00-c", "3", RunStatuses.Failed, RunEndKinds.Preflight));

        stuck.Select(s => s.TicketId).Should().Equal(["3"], "a closed or landed ticket is never released, so it is not listed");
    }

    [Fact]
    public async Task ConfigChangeClock_NewestVersionRow_IsTheLastChange()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        var clock = new DbConfigChangeClock(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        (await clock.LastChangedAtAsync(CancellationToken.None)).Should().BeNull("nothing was ever stored");

        await using (var db = Db())
        {
            db.Add(new ConfigEntityVersion { EntityType = "agent", EntityId = "a", Version = 1, Doc = "{}", ChangedBy = "x" });
            await db.SaveChangesAsync();
        }

        (await clock.LastChangedAtAsync(CancellationToken.None)).Should().NotBeNull();
    }
}
