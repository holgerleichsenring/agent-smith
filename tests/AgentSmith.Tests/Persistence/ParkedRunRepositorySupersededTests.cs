using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-16-86e6b: a park is superseded once a newer code run of its ticket started work.</summary>
public sealed class ParkedRunRepositorySupersededTests : IDisposable
{
    private const string Park = "2026-10-16T14-56-33-0007";
    private const string Newer = "2026-10-16T15-05-05-0008";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-16T15:00:00Z");
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ParkedRunRepository_NewerAttemptWithAStep_ListsTheParkAsSuperseded()
    {
        await SeedAsync(Waiting(Park), Run(Newer, "failed"), Step(Newer));

        var parks = await ListAsync();

        parks.Should().ContainSingle().Which.Should().Be(
            new AgentSmith.Contracts.Models.SupersededPark(Park, "p1", "42", Newer));
    }

    [Fact]
    public async Task ParkedRunRepository_NewerRowWithoutAStep_DoesNotListThePark()
    {
        await SeedAsync(Waiting(Park), Run(Newer, "cancelled"));

        (await ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ParkedRunRepository_OtherTicketsNewerRun_DoesNotListThePark()
    {
        await SeedAsync(Waiting(Park), Run(Newer, "failed", ticket: "43"), Step(Newer));

        (await ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ParkedRunRepository_ResumedCheckpointOrQueuedResume_DoesNotListThePark()
    {
        await SeedAsync(Waiting(Park), Run(Newer, "failed"), Step(Newer),
            new RunCheckpoint { RunId = Park, Project = "p1", TicketId = "42", Pipeline = "code", ResumedAt = T });
        var other = "2026-10-16T14-00-00-0006";
        await SeedAsync(Waiting(other), new QueuedTicket
        {
            Project = "p1", TicketId = "42", Pipeline = "code", Platform = "github", ReservedRunId = other,
            Reason = "resuming", EnqueuedAt = T, IsResume = true,
        });

        (await ListAsync()).Should().BeEmpty();
    }

    private async Task<IReadOnlyList<AgentSmith.Contracts.Models.SupersededPark>> ListAsync()
    {
        using var ctx = Context();
        return await new ParkedRunRepository(ctx).ListSupersededAsync(CancellationToken.None);
    }

    private async Task SeedAsync(params object[] rows)
    {
        using var ctx = Context();
        ctx.AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    private static Run Waiting(string id) => Run(id, "waiting_for_input");

    private static Run Run(string id, string status, string ticket = "42") => new()
    {
        Id = id, Project = "p1", Pipeline = "code", TicketId = ticket, Platform = "github", Status = status,
        StartedAt = T, FinishedAt = status == "waiting_for_input" ? null : T.AddMinutes(30),
    };

    private static RunStep Step(string runId) =>
        new() { RunId = runId, StepIndex = 1, StepName = "Fetching ticket", Status = "success" };

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
