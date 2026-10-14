using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-13-8762o: event 88 through the composition root's own registration, over a migrated SQLite
/// store — one pending row per trail position, one pending pointer per memory file per run, a verdict a
/// replay never resets, and the table arriving by one migration of one name in both sets.
/// </summary>
public sealed class RunDefaultDecisionProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
    private const string RunId = "2026-10-14T1100-defaults";
    private const string Migration = "AddRunDefaultDecisions";
    private const string Pointer = "shop/.agentsmith/memory/ask-first.md";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public RunDefaultDecisionProjectionTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Mock.Of<ITicketActivityNudge>());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RunDefaultDecisionProjection_InsertsPendingRow()
    {
        await ApplyAsync(Decision(), seq: 3);

        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        (row.SpecId, row.Question, row.Chosen, row.Why, row.HoldsSeries, row.Kind, row.Verdict, row.EventSeq, row.TakenAt)
            .Should().Be(("2026-10-14-5678a", "Which queue?", "the bus", "it exists", true,
                DefaultDecisionKinds.Decision, DefaultDecisionVerdicts.Pending, 3L, T));
        AgentSmith.Contracts.Runs.RunStoryJson.TryDeserialize<List<string>>(row.AlternativesJson)!
            .Should().Equal("a queue", "polling");
        (row.PointerPath, row.Conversation, row.VerdictBy, row.VerdictAt, row.VerdictNote)
            .Should().Be(((string?)null, "conversation-7", (string?)null, (DateTimeOffset?)null, (string?)null));
    }

    [Fact]
    public async Task RunDefaultDecisionProjection_ReplayedPosition_WritesOnce()
    {
        await ApplyAsync(Decision(), seq: 3);
        await ApplyAsync(Decision(), seq: 3);

        (await RowsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task RunDefaultDecisionProjection_ReplayAfterVerdict_KeepsTheVerdict()
    {
        await ApplyAsync(Decision(), seq: 3);
        await using (var ctx = new AgentSmithDbContext(Options()))
        {
            var row = await ctx.Set<RunDefaultDecision>().SingleAsync(r => r.RunId == RunId);
            (row.Verdict, row.VerdictBy, row.VerdictAt, row.VerdictNote) =
                (DefaultDecisionVerdicts.Overruled, "alice", T.AddHours(1), "use a queue");
            await ctx.SaveChangesAsync();
        }

        await ApplyAsync(Decision(), seq: 3);

        var kept = (await RowsAsync()).Should().ContainSingle().Subject;
        (kept.Verdict, kept.VerdictBy, kept.VerdictNote).Should().Be((DefaultDecisionVerdicts.Overruled, "alice", "use a queue"));
    }

    [Fact]
    public async Task RunDefaultDecisionProjection_SecondPendingProposalSamePointer_WritesOnce()
    {
        await ApplyAsync(Proposal(Pointer), seq: 1);
        await ApplyAsync(Proposal(Pointer), seq: 2);
        await ApplyAsync(Proposal("shop/.agentsmith/memory/other.md"), seq: 3);

        (await RowsAsync()).Select(r => (r.PointerPath, r.EventSeq))
            .Should().BeEquivalentTo(new[] { ((string?)Pointer, (long?)1L), ("shop/.agentsmith/memory/other.md", 3L) });
    }

    [Fact]
    public async Task RunDeletion_DeletesDefaultDecisions()
    {
        await ApplyAsync(Decision(), seq: 1);
        await ApplyAsync(Proposal(Pointer), seq: 2);

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunDeletionRepository(ctx).DeleteAsync(RunId, CancellationToken.None);

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public void RunDefaultDecisions_SqlServerMigration_UniqueIndexFilteredOnEventSeq()
    {
        using (var sqlite = new AgentSmithDbContext(Options()))
            sqlite.Model.FindEntityType(typeof(RunDefaultDecision))!.GetTableName().Should().Be("RunDefaultDecisions");

        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer })
            Named(provider).Should().ContainSingle().Which.Should().Be(Migration);

        using var sqlServer = new AgentSmithDbContext(ProviderOptions(PersistenceProvider.SqlServer));
        var entity = sqlServer.Model.FindEntityType(typeof(RunDefaultDecision))!;
        var identity = entity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["RunId", "EventSeq"]));
        (identity.IsUnique, identity.GetFilter()).Should().Be((true, "[EventSeq] IS NOT NULL"));
        entity.GetIndexes().Should().Contain(i => i.Properties.Single().Name == nameof(RunDefaultDecision.Conversation)
            && i.Properties.Count == 1);
    }

    [Fact]
    public async Task Persistence_RunDefaultDecisions_RoundTripOnSqlite()
    {
        var read = await RoundTripAsync(() => new AgentSmithDbContext(Options()));

        (read.Kind, read.PointerPath, read.Verdict).Should().Be((DefaultDecisionKinds.MemoryProposal, Pointer, DefaultDecisionVerdicts.Pending));
    }

    [RequiresSqlServerFact]
    public async Task Persistence_RunDefaultDecisions_RoundTripOnSqlServer()
    {
        await using var scratch = await ScratchSqlServer.MigratedAsync(RequiresSqlServerFactAttribute.ConnectionString!, "defaults");
        try
        {
            var options = new DbContextOptionsBuilder<AgentSmithDbContext>()
                .UseSqlServer(scratch.Database.GetConnectionString()).Options;
            var read = await RoundTripAsync(() => new AgentSmithDbContext(options));

            (read.Kind, read.PointerPath, read.Verdict).Should().Be((DefaultDecisionKinds.MemoryProposal, Pointer, DefaultDecisionVerdicts.Pending));
        }
        finally
        {
            await scratch.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public void EventTypeResolver_ResolvesDefaultDecisionTaken()
    {
        ((int)EventType.DefaultDecisionTaken).Should().Be(88);
        EventTypeResolver.Resolve(EventType.DefaultDecisionTaken).Should().Be(typeof(DefaultDecisionTakenEvent));
    }

    private static async Task<RunDefaultDecision> RoundTripAsync(Func<AgentSmithDbContext> context)
    {
        var runId = $"defaults-{Guid.NewGuid():N}";
        await using (var write = context())
        {
            write.Add(RunDefaultDecisionProjection.RowOf(Proposal(Pointer) with { RunId = runId }, 9));
            await write.SaveChangesAsync();
        }
        await using var read = context();
        return await read.Set<RunDefaultDecision>().AsNoTracking().SingleAsync(r => r.RunId == runId);
    }

    private static DefaultDecisionTakenEvent Decision() => new(
        RunId, T, "2026-10-14-5678a", "Which queue?", "the bus", ["a queue", "polling"], "it exists", true,
        DefaultDecisionKinds.Decision, null, "conversation-7");

    private static DefaultDecisionTakenEvent Proposal(string pointer) => new(
        RunId, T, null, "Ratify this proposed feedback memory?", "ask-first", [], string.Empty, false,
        DefaultDecisionKinds.MemoryProposal, pointer, null);

    private static IReadOnlyList<string> Named(PersistenceProvider provider)
    {
        using var db = new AgentSmithDbContext(ProviderOptions(provider));
        return [.. db.Database.GetMigrations().Select(id => id.Split('_', 2).Last()).Where(name => name == Migration)];
    }

    private static DbContextOptions<AgentSmithDbContext> ProviderOptions(PersistenceProvider provider)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = provider == PersistenceProvider.Sqlite
                ? "Data Source=:memory:"
                : "Server=localhost;Database=x;TrustServerCertificate=True",
        });
        return builder.Options;
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(DefaultDecisionTakenEvent ev, long seq)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq, CancellationToken.None);
    }

    private async Task<List<RunDefaultDecision>> RowsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunDefaultDecision>().Where(r => r.RunId == RunId).ToListAsync();
    }
}
