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
/// 2026-10-13-8762e: event 87 through the composition root's own registration, over a migrated SQLite
/// store — one row per trail position, the lists as JSON, the table arriving by one migration of one name
/// in both sets, and the rows living exactly as long as the run.
/// </summary>
public sealed class RunSpecReviewRoundProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private const string RunId = "2026-10-14T0900-review";
    private const string Migration = "AddRunSpecReviewRounds";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public RunSpecReviewRoundProjectionTests()
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
    public async Task RunSpecReviewRoundProjection_SameEventTwice_OneRow()
    {
        await ApplyAsync(Round(), seq: 4);
        await ApplyAsync(Round(), seq: 4);

        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        (row.SpecId, row.Scope, row.Round, row.Reviewer, row.Verdict, row.EventSeq, row.RevisionNumber)
            .Should().Be(("2026-10-14-1234a", SpecReviewScopes.Spec, 2, 1, SpecReviewVerdicts.BuildWithCuts, 4L, 3));
        row.CutsJson.Should().Contain("\"register\":\"goal\"").And.Contain("api/src/A.cs:3 [A1]");
        AgentSmith.Contracts.Runs.RunStoryJson.TryDeserialize<List<SpecReviewFinding>>(row.FindingsJson)!
            .Should().Equal(new SpecReviewFinding("no caller reads it", "api search:'Reads' [A2]"));
        row.EvidenceProblemsJson.Should().Contain("\"claim\":\"it exists\"");
        row.ResiduesJson.Should().Contain("\"quote\":\"the four word run\"");
    }

    [Fact]
    public async Task RunDeletion_DeletesReviewRounds()
    {
        await ApplyAsync(Round(), seq: 1);

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunDeletionRepository(ctx).DeleteAsync(RunId, CancellationToken.None);

        (await RowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public void RunSpecReviewRound_Migrations_ApplyOnSqliteAndSqlServer()
    {
        using (var sqlite = new AgentSmithDbContext(Options()))
            sqlite.Model.FindEntityType(typeof(RunSpecReviewRound))!.GetTableName().Should().Be("RunSpecReviewRounds");

        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer })
            Named(provider).Should().ContainSingle().Which.Should().Be(Migration);

        using var sqlServer = new AgentSmithDbContext(ProviderOptions(PersistenceProvider.SqlServer));
        var identity = sqlServer.Model.FindEntityType(typeof(RunSpecReviewRound))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["RunId", "EventSeq"]));
        (identity.IsUnique, identity.GetFilter()).Should().Be((true, "[EventSeq] IS NOT NULL"));
    }

    [Fact]
    public void EventTypeResolver_ResolvesSpecReviewRound()
    {
        ((int)EventType.SpecReviewRound).Should().Be(87);
        EventTypeResolver.Resolve(EventType.SpecReviewRound).Should().Be(typeof(SpecReviewRoundEvent));
    }

    private static SpecReviewRoundEvent Round() => new(
        RunId, T, "2026-10-14-1234a", SpecReviewScopes.Spec, 2, 1, SpecReviewVerdicts.BuildWithCuts,
        [new SpecReviewCut(SpecRegisterNames.Goal, "the four word run", "a new run", "api/src/A.cs:3 [A1]")],
        [new SpecReviewFinding("no caller reads it", "api search:'Reads' [A2]")],
        [new SpecEvidenceProblem("it exists", "not a file (missing, or a directory): src/B.cs")],
        [new SpecReviewResidue(SpecRegisterNames.Done, "the four word run")], RevisionNumber: 3);

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

    private async Task ApplyAsync(SpecReviewRoundEvent ev, long seq)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq, CancellationToken.None);
    }

    private async Task<List<RunSpecReviewRound>> RowsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<RunSpecReviewRound>().Where(r => r.RunId == RunId).ToListAsync();
    }
}
