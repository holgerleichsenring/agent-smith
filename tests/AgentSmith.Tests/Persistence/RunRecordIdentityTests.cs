using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-08-25-61f1: a run's recorded facts cannot be written twice. Every row a run event
/// produces carries the event's position in that run's trail, the store holds at most one
/// row per (run, position), and the writer that meets a position the store already has
/// keeps the rest of its batch instead of losing it to an exception.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunRecordIdentityTests : IDisposable
{
    private const string RunId = "run-1";
    private const long Seq = 7;
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-08-25T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly TimeProvider _clock = TimeProvider.System;

    public RunRecordIdentityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = Context();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task TrailRow_WrittenTwiceForOneRunAndSequence_IsRecordedOnce()
    {
        await using var ctx = Context();
        ctx.Add(TrailRow(Seq));
        await ctx.SaveChangesAsync();

        await using var second = Context();
        second.Add(TrailRow(Seq));

        var write = async () => await second.SaveChangesAsync();

        await write.Should().ThrowAsync<DbUpdateException>(
            "the store refuses a second row at one run position rather than accepting it");
    }

    [Fact]
    public async Task TrailRow_WrittenTwice_TheWriterKeepsItsBatchAndDoesNotThrowAway()
    {
        var provider = BuildProvider();
        var projector = provider.GetRequiredService<RunDbProjector>();
        await projector.ProjectAsync(Tool("first"), CancellationToken.None);
        await projector.ProjectAsync(Tool("second"), CancellationToken.None);

        // A row lands at the position the buffer already handed out — the shape a replay,
        // a restart racing a drain or a second writer produces.
        await using (var racing = Context())
        {
            racing.Add(TrailRow(0));
            await racing.SaveChangesAsync();
        }

        await projector.FlushAllAsync(CancellationToken.None);

        await using var ctx = Context();
        ctx.RunEvents.Count(e => e.Seq == 0).Should().Be(1, "the position is held once");
        ctx.RunEvents.Count(e => e.Seq == 1).Should().Be(1,
            "the rest of the batch is written — refusing one row must not lose the others");
    }

    [Fact]
    public async Task LlmCallRow_WrittenTwiceForOneCall_IsRecordedOnce()
    {
        await ApplyTwiceAsync(new LlmCallFinishedEvent(RunId, "claude", "coding-agent", 100, 20, 0.5m, 500, T0));

        await using var ctx = Context();
        ctx.RunLlmCalls.Should().HaveCount(1);
        ctx.RunLlmCalls.Single().EventSeq.Should().Be(Seq);
    }

    [Fact]
    public async Task LlmCallRow_WrittenTwiceForOneCall_TheRunTotalCountsItOnce()
    {
        await SeedRunAsync();

        await ApplyTwiceAsync(new LlmCallFinishedEvent(RunId, "claude", "coding-agent", 100, 20, 0.5m, 500, T0));

        await using var ctx = Context();
        ctx.Runs.Single().CostTotalUsd.Should().Be(0.5m, "a call projected twice is spent once");
    }

    [Fact]
    public async Task StepRow_WrittenTwiceForOneStep_IsRecordedOnce()
    {
        await ApplyTwiceAsync(new StepStartedEvent(RunId, 3, "build", 9, T0, "Build"));

        await using var ctx = Context();
        ctx.RunSteps.Should().HaveCount(1);
        ctx.RunSteps.Single().EventSeq.Should().Be(Seq);
    }

    [Fact]
    public async Task DecisionRow_WrittenTwiceForOneDecision_IsRecordedOnce()
    {
        await ApplyTwiceAsync(new DecisionLoggedEvent(RunId, "tooling", "sqlite", "postgres", "smallest", T0));

        await using var ctx = Context();
        ctx.RunDecisions.Should().HaveCount(1);
    }

    [Fact]
    public async Task RecordWithNoPosition_IsNeverSuppressed()
    {
        var applier = RunEventAppliers.Default();
        var decision = new DecisionLoggedEvent(RunId, "tooling", "sqlite", "postgres", "smallest", T0);

        await using (var first = Context()) await applier.ApplyAsync(first, decision, CancellationToken.None);
        await using (var second = Context()) await applier.ApplyAsync(second, decision, CancellationToken.None);

        await using var ctx = Context();
        ctx.RunDecisions.Should().HaveCount(2,
            "absent an identity there is nothing to compare — refusing on a guess would drop a real record");
    }

    public void Dispose() => _connection.Dispose();

    private async Task ApplyTwiceAsync(AgentSmith.Contracts.Events.RunEvent ev)
    {
        var applier = RunEventAppliers.Default();
        await using (var first = Context()) await applier.ApplyAsync(first, ev, Seq, CancellationToken.None);
        await using (var second = Context()) await applier.ApplyAsync(second, ev, Seq, CancellationToken.None);
    }

    private async Task SeedRunAsync()
    {
        await using var ctx = Context();
        ctx.Add(new Run { Id = RunId, Pipeline = "fix-bug", Project = "p", TicketId = "t", Status = "running", StartedAt = T0 });
        await ctx.SaveChangesAsync();
    }

    private static AgentSmith.Infrastructure.Persistence.Entities.RunEvent TrailRow(long seq) =>
        new() { RunId = RunId, Seq = seq, Type = nameof(EventType.ToolCall), Timestamp = T0 };

    private static ToolCallEvent Tool(string name) => new(RunId, name, 12, T0, name);

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private AgentSmithDbContext Context() => new(Options());

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Context());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<RunDbProjector>();
        return services.BuildServiceProvider();
    }
}
