using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-08-28-b883: a money column returns what it was handed. SQL Server typed the three
/// money columns decimal(18,2), which rounds every per-call cost a model produces to 0.00.
/// The round trip runs on SQLite always, and on SQL Server too whenever
/// AGENTSMITH_TEST_DB_CONNSTR names a SERVER, on which this creates and drops a database
/// of its own — the reading the archive tests already apply to it. The schema assertion
/// reads the model and the shipped snapshot, so it runs everywhere.
/// </summary>
public sealed class MoneyPrecisionPersistenceTests
{
    private const string SqliteProvider = "sqlite";
    private const string SqlServerProvider = "sqlserver";
    private const string SqlServerConnectionVariable = "AGENTSMITH_TEST_DB_CONNSTR";

    /// <summary>The smallest per-call cost an observed live run produced; decimal(18,2) stores 0.00.</summary>
    private const decimal CallCost = 0.0003219m;
    private const decimal RunTotal = 0.5843112m;
    private const decimal BudgetCap = 12.3456789m;

    private static string? SqlServerConnection =>
        Environment.GetEnvironmentVariable(SqlServerConnectionVariable);

    public static TheoryData<string> ConfiguredProviders()
    {
        var providers = new TheoryData<string> { SqliteProvider };
        if (!string.IsNullOrWhiteSpace(SqlServerConnection)) providers.Add(SqlServerProvider);
        return providers;
    }

    [Theory]
    [MemberData(nameof(ConfiguredProviders))]
    public async Task Cost_APerCallFraction_ReadsBackExactlyOnEveryProvider(string provider)
    {
        var stored = await RoundTripAsync(provider);

        stored.Call.Should().Be(CallCost, "a call costs a fraction of a cent and the column must keep it");
    }

    [Theory]
    [MemberData(nameof(ConfiguredProviders))]
    public async Task Cost_ARunTotal_ReadsBackExactlyOnEveryProvider(string provider)
    {
        var stored = await RoundTripAsync(provider);

        stored.Total.Should().Be(RunTotal, "a run total is the sum of those fractions");
    }

    [Theory]
    [MemberData(nameof(ConfiguredProviders))]
    public async Task Cost_ABudgetCap_ReadsBackExactlyOnEveryProvider(string provider)
    {
        var stored = await RoundTripAsync(provider);

        stored.Cap.Should().Be(BudgetCap, "the cap is the same kind of number as the spend it fences");
    }

    [Fact]
    public void Migration_TheSqlServerSchema_TypesMoneyWiderThanTwoPlaces()
    {
        using var ctx = NewContext(SqlServerProvider, sqlite: null);

        foreach (var (entity, property) in MoneyColumns)
            ctx.Model.FindEntityType(entity)!.FindProperty(property)!.GetScale()
                .Should().BeGreaterThan(2, "{0}.{1} holds a per-call cost", entity.Name, property);

        File.ReadAllText(SqlServerSnapshotPath).Should().NotContain("decimal(18,2)",
            "the shipped snapshot carries the widened type, or no migration was generated for it");
    }

    private static readonly (Type Entity, string Property)[] MoneyColumns =
        [(typeof(Run), nameof(Run.CostTotalUsd)), (typeof(Run), nameof(Run.BudgetCapUsd)),
            (typeof(RunLlmCall), nameof(RunLlmCall.CostUsd))];

    private static string SqlServerSnapshotPath => Path.Combine(
        ArchitectureSources.BackendRoot, "AgentSmith.Infrastructure.Persistence.SqlServer",
        "Migrations", "AgentSmithDbContextModelSnapshot.cs");

    private static async Task<(decimal Call, decimal Total, decimal? Cap)> RoundTripAsync(string provider)
    {
        // SQL Server gets a migrated database of this run's own — the variable names a
        // SERVER, the same reading the archive's own SQL Server test applies to it.
        if (provider != SqliteProvider)
        {
            await using var scratch = await ScratchSqlServer.MigratedAsync(SqlServerConnection!, "money");
            try
            {
                return await WriteAndReadAsync(() => new AgentSmithDbContext(Options(scratch)));
            }
            finally
            {
                await scratch.Database.EnsureDeletedAsync();
            }
        }

        using var sqlite = MigratedStoreTemplate.OpenCopy();
        return await WriteAndReadAsync(() => NewContext(provider, sqlite));
    }

    private static DbContextOptions<AgentSmithDbContext> Options(AgentSmithDbContext migrated)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseSqlServer(migrated.Database.GetConnectionString());
        return builder.Options;
    }

    private static async Task<(decimal Call, decimal Total, decimal? Cap)> WriteAndReadAsync(
        Func<AgentSmithDbContext> context)
    {
        var runId = $"money-{Guid.NewGuid():N}";
        await using (var write = context())
        {
            write.Runs.Add(NewRun(runId));
            write.RunLlmCalls.Add(new RunLlmCall { RunId = runId, Model = "test-model", CostUsd = CallCost });
            await write.SaveChangesAsync();
        }

        await using var read = context();
        var run = await read.Runs.AsNoTracking().SingleAsync(r => r.Id == runId);
        var call = await read.RunLlmCalls.AsNoTracking().SingleAsync(c => c.RunId == runId);
        return (call.CostUsd, run.CostTotalUsd, run.BudgetCapUsd);
    }

    // A SQL Server context exposes its model without reaching a server.
    private static AgentSmithDbContext NewContext(string provider, SqliteConnection? sqlite)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        if (provider == SqliteProvider) builder.UseSqlite(sqlite!);
        else builder.UseSqlServer(SqlServerConnection ?? "Server=localhost;Database=x;TrustServerCertificate=True");
        return new AgentSmithDbContext(builder.Options);
    }

    private static Run NewRun(string runId) => new()
    {
        Id = runId, Project = "money-precision", Pipeline = "fix-bug", TicketId = runId,
        Status = "success", StartedAt = DateTimeOffset.UtcNow, CostTotalUsd = RunTotal, BudgetCapUsd = BudgetCap,
    };
}
