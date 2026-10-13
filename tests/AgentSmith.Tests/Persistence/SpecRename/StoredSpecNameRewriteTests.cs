using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ab: a store holding every surface the old release wrote in the phase names holds
/// only spec names after the rewrite, reads back as it did, and a second pass changes nothing.
/// 2026-10-12-967ah: with every reader of the old names gone, a store last written before 967ab —
/// its spec ids still in the old column — converts through the migrations and the frozen table.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class StoredSpecNameRewriteTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();


    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task StoreRewrite_OldShapeRows_ReadBackAsSpec()
    {
        await using var db = await SeededAsync(Context);
        (await SpecNamedStoreAssertions.TextsAsync(db)).Where(t => t!.Contains("hase") || t.StartsWith("spec_record:"))
            .Should().HaveCount((await SpecNamedStoreAssertions.TextsAsync(db)).Count, "the seed is the old shape throughout");

        await Rewrite().RewriteAsync(db, CancellationToken.None);

        await SpecNamedStoreAssertions.HoldsOnlySpecNamesAsync(db);
        await SpecNamedStoreAssertions.ReadsBackAsTheOldShapeDidAsync(db);
    }

    [Fact]
    public async Task StoreRewrite_PrePhaseStore_ConvertsWithoutReaderAliases()
    {
        await using var empty = new SqliteConnection("Data Source=:memory:");
        await empty.OpenAsync();
        await using var db = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(empty).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeSpecIdColumns);
        await db.Database.ExecuteSqlRawAsync(OldShapeStore.PreSpecIdRowsSql);
        await migrator.MigrateAsync();
        await OldShapeStore.SeedAsync(db, withStepRows: false);
        db.ChangeTracker.Clear();

        await Rewrite().RewriteAsync(db, CancellationToken.None);

        await SpecNamedStoreAssertions.HoldsOnlySpecNamesAsync(db);
        await SpecNamedStoreAssertions.ReadsBackAsTheOldShapeDidAsync(db);
    }

    [RequiresSqlServerFact]
    public async Task StoreRewrite_OldShapeRows_ReadBackAsSpec_OnSqlServer()
    {
        await using var scratch = await ScratchSqlServer.MigratedAsync(RequiresSqlServerFactAttribute.ConnectionString!, "specname");
        try
        {
            await OldShapeStore.SeedAsync(scratch);
            await Rewrite().RewriteAsync(scratch, CancellationToken.None);

            await SpecNamedStoreAssertions.HoldsOnlySpecNamesAsync(scratch);
            await SpecNamedStoreAssertions.ReadsBackAsTheOldShapeDidAsync(scratch);
        }
        finally
        {
            await scratch.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task StoreRewrite_RunTwice_ChangesNothing()
    {
        await using var db = await SeededAsync(Context);
        await Rewrite().RewriteAsync(db, CancellationToken.None);
        var first = await StoreText.DumpAsync(db);

        var second = await Rewrite().RewriteAsync(db, CancellationToken.None);

        second.Values.Should().OnlyContain(rows => rows == 0, "a rewritten row has no old name left to find");
        (await StoreText.DumpAsync(db)).Should().Equal(first);
    }

    [Fact]
    public async Task StoreRewrite_LlmEventPhase_Untouched()
    {
        await using var db = await SeededAsync(Context);

        await Rewrite().RewriteAsync(db, CancellationToken.None);

        var llm = await db.RunEvents.AsNoTracking().SingleAsync(e => e.Seq == OldShapeStore.LlmCall.Seq);
        llm.PayloadJson.Should().Be(OldShapeStore.LlmCall.PayloadJson).And.Contain("\"Phase\":\"Implementation\"");
        llm.Phase.Should().Be("Implementation");
    }

    private const string MigrationBeforeSpecIdColumns = "AddTicketSeriesExecutedIds";

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static async Task<AgentSmithDbContext> SeededAsync(Func<AgentSmithDbContext> context)
    {
        await using (var seed = context()) await OldShapeStore.SeedAsync(seed);
        return context();
    }

    private static StoredSpecNameRewrite Rewrite() =>
        new ServiceCollection().AddLogging().AddRunStoreRepair().BuildServiceProvider()
            .GetRequiredService<StoredSpecNameRewrite>();
}
