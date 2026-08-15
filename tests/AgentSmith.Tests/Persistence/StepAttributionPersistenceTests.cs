using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0388a: the producer's step stamp lands on the child projections, so per-step
/// aggregates are one indexed query instead of a client-side fold. Pre-phase
/// payloads carry no stamp and persist as unattributed — the applier never
/// infers a step for them. Proven on a real SQLite engine.
/// </summary>
public sealed class StepAttributionPersistenceTests : IDisposable
{
    // The last migration before p0388a's — the "before" state a live database is in.
    private const string MigrationBeforeStepAttribution = "AddRunMetrics";

    private readonly SqliteConnection _connection;
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-07-29T09:00:00Z");

    public StepAttributionPersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private AgentSmithDbContext Migrated()
    {
        var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
        return ctx;
    }

    private async Task ApplyAsync(Contracts.Events.RunEvent runEvent)
    {
        await using var ctx = Migrated();
        await new RunEventApplier(new(), new(), new(), new(), new(), new(), new()).ApplyAsync(ctx, runEvent, CancellationToken.None);
    }

    [Fact]
    public async Task Applier_LlmCallFinished_PersistsStepIndexOnRunLlmCall()
    {
        await ApplyAsync(new LlmCallFinishedEvent(
            "run-1", "claude", "coding-agent", 100, 20, 0.01m, 500, _now) { OriginStepIndex = 5 });

        await using var ctx = Migrated();
        ctx.RunLlmCalls.Single().StepIndex.Should().Be(5);
    }

    [Fact]
    public async Task Applier_DecisionLogged_PersistsStepIndexOnRunDecision()
    {
        await ApplyAsync(new DecisionLoggedEvent(
            "run-1", "tooling", "sqlite", "postgres", "smallest footprint", _now) { OriginStepIndex = 2 });

        await using var ctx = Migrated();
        ctx.RunDecisions.Single().StepIndex.Should().Be(2);
    }

    // p0388c: the event always carried the category; the projection did not, so
    // the operator-facing notes lost it when they moved off the live buffer.
    [Fact]
    public async Task Applier_DecisionLogged_PersistsCategoryOnRunDecision()
    {
        await ApplyAsync(new DecisionLoggedEvent(
            "run-1", "tooling", "sqlite", "postgres", "smallest footprint", _now));

        await using var ctx = Migrated();
        var decision = ctx.RunDecisions.Single();
        decision.Category.Should().Be("tooling");
        decision.Name.Should().Be("sqlite");
        decision.Reason.Should().Be("smallest footprint");
    }

    [Fact]
    public async Task Applier_PrePhasePayloadWithoutStepIndex_PersistsNullWithoutFailing()
    {
        // A payload written by a pre-p0388a producer: no originStepIndex field at all.
        const string prePhasePayload =
            """
            {"RunId":"run-1","Model":"claude","Role":"coding-agent","TokensIn":100,
             "TokensOut":20,"CostUsd":0.01,"DurationMs":500,
             "Timestamp":"2026-07-29T09:00:00+00:00","Type":11}
            """;
        var replayed = new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer().DeserializeRaw(
            nameof(EventType.LlmCallFinished), prePhasePayload);
        replayed.Should().NotBeNull();

        await ApplyAsync(replayed!);

        await using var ctx = Migrated();
        ctx.RunLlmCalls.Single().StepIndex.Should().BeNull();
    }

    [Fact]
    public async Task Migration_ExistingRows_KeepNullStepIndex()
    {
        await using (var before = new AgentSmithDbContext(Options()))
        {
            await before.GetService<IMigrator>().MigrateAsync(MigrationBeforeStepAttribution);
            await before.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO RunEvents (RunId, Seq, Type, Timestamp, CreatedAt, UpdatedAt)
                VALUES ('run-old', 1, 'StepStarted', '2026-07-01 00:00:00+00:00',
                        '2026-07-01 00:00:00+00:00', '2026-07-01 00:00:00+00:00')
                """);
        }

        await using var after = Migrated();

        after.RunEvents.Single().StepIndex.Should().BeNull();
    }

    public void Dispose() => _connection.Dispose();
}
