using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-08-28-2af6: the pieces the archive is assembled from — the schema head both
/// providers can agree on, the write order the model dictates, and the row codec.
/// </summary>
public sealed class DataArchiveSchemaTests : IDisposable
{
    private readonly SqliteConnection _store = MigratedStoreTemplate.OpenCopy();

    /// <summary>
    /// The load-bearing one. The two providers keep separate migration assemblies with
    /// disjoint histories and different timestamp prefixes on the SAME migration, so an
    /// import that compared recorded ids would refuse every SQLite-to-SQL-Server move —
    /// which is the entire journey the archive exists for.
    /// </summary>
    [Fact]
    public void Manifest_TheTwoProvidersSchemaHeads_CompareEqual()
    {
        var head = new MigrationHeadName();

        var sqlite = head.Of(Migrations(PersistenceProvider.Sqlite));
        var sqlServer = head.Of(Migrations(PersistenceProvider.SqlServer));

        sqlite.Should().Be(sqlServer, "an archive must be able to cross the providers");
        sqlite.Should().NotBeEmpty();
        Migrations(PersistenceProvider.Sqlite).Last()
            .Should().NotBe(Migrations(PersistenceProvider.SqlServer).Last(),
                "the ids differ, which is exactly why the NAME is what is compared");
    }

    [Fact]
    public void MigrationHeadName_TheNewestId_LosesItsProviderLocalPrefix()
    {
        var head = new MigrationHeadName();

        head.Of(["20260607082443_InitialCreate", "20260826083039_AddObservedCallers"])
            .Should().Be("AddObservedCallers");
    }

    [Fact]
    public void MigrationHeadName_AStoreWithNoMigrations_HasNoSchemaState() =>
        new MigrationHeadName().Of([]).Should().BeEmpty();

    [Fact]
    public void ArchiveTableOrder_EveryTable_FollowsTheTablesItReferences()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var ordered = new ArchiveTableOrder().Of(db.Model);

        ordered.Should().HaveCount(22);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in ordered)
        {
            foreach (var principal in type.GetForeignKeys().Select(fk => fk.PrincipalEntityType))
                if (principal != type)
                    placed.Should().Contain(principal.GetTableName()!,
                        "a table is written after everything it references");
            placed.Add(type.GetTableName()!);
        }
    }

    [Fact]
    public void ArchiveRowCodec_ARowWithNullsAndAwkwardText_RoundTripsThroughOneLine()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var type = db.Model.FindEntityType(typeof(RunArtifact))!;
        var codec = new ArchiveRowCodec();
        var row = new RunArtifact
        {
            Id = 7,
            RunId = "r",
            Kind = "k",
            Content = null,
            CreatedAt = FullStoreSeed.SeededAt,
            UpdatedAt = FullStoreSeed.SeededAt,
        };

        var line = codec.Encode(type, row);
        var back = (RunArtifact)codec.Decode(type, line);

        line.Should().NotContain("\n", "one row is one line, whatever its text holds");
        back.Should().BeEquivalentTo(row);
    }

    [Fact]
    public void ArchiveRowCodec_TheSameAmountAtTwoScales_EncodesToOneLine()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var type = db.Model.FindEntityType(typeof(RunLlmCall))!;
        var codec = new ArchiveRowCodec();

        // What each provider hands back for one stored amount: SQL Server returns its
        // column's scale, SQLite's text column returns what was written.
        var wide = codec.Encode(type, Call(13.3400000000m));
        var narrow = codec.Encode(type, Call(13.34m));

        wide.Should().Be(narrow,
            "the same data must yield the same archive whichever provider it was read from");
        wide.Should().Contain("\"CostUsd\":13.34");
        ((RunLlmCall)codec.Decode(type, wide)).CostUsd.Should().Be(13.34m);

        RunLlmCall Call(decimal cost) => new()
        {
            Id = 1,
            RunId = "r",
            CostUsd = cost,
            CreatedAt = FullStoreSeed.SeededAt,
            UpdatedAt = FullStoreSeed.SeededAt,
        };
    }

    [Fact]
    public void ArchiveRowCodec_AnAmountFinerThanTheOldScale_KeepsEveryDigit()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var type = db.Model.FindEntityType(typeof(RunLlmCall))!;
        var codec = new ArchiveRowCodec();
        var row = new RunLlmCall
        {
            Id = 1,
            RunId = "r",
            CostUsd = 0.000000025m,
            CreatedAt = FullStoreSeed.SeededAt,
            UpdatedAt = FullStoreSeed.SeededAt,
        };

        var back = (RunLlmCall)codec.Decode(type, codec.Encode(type, row));

        back.CostUsd.Should().Be(0.000000025m,
            "dropping trailing zeros must not drop a digit that carries value");
    }

    [Fact]
    public void ArchiveRowCodec_ALineMissingAColumn_SaysWhichOne()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var type = db.Model.FindEntityType(typeof(RunArtifact))!;

        var act = () => new ArchiveRowCodec().Decode(type, """{"Id":1}""");

        act.Should().Throw<DataArchiveException>().WithMessage("*RunArtifacts*Content*");
    }

    [Fact]
    public void GeneratedKeyProperty_TheTablesTheProviderGeneratesKeysFor_AreTheTwentyWithLongIds()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var generated = new GeneratedKeyProperty();

        DataArchiveHarness.Tables(db).Count(t => generated.Of(t) is not null).Should().Be(20);
        generated.Of(db.Model.FindEntityType(typeof(Run))!).Should().BeNull("a run carries its own id");
        generated.Of(db.Model.FindEntityType(typeof(RunEvent))!).Should().NotBeNull();
    }

    [Fact]
    public async Task IdentityInsertSwitch_OnSqlite_AsksTheProviderForNothing()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var type = db.Model.FindEntityType(typeof(RunEvent))!;
        var switching = new IdentityInsertSwitch(new GeneratedKeyProperty(), new NullLogger<IdentityInsertSwitch>());

        switching.IsRequiredFor(db, type).Should().BeFalse("SQLite takes a copied key as it is");
        await switching.EnableAsync(db, type, CancellationToken.None);
        await switching.DisableAsync(db, type, CancellationToken.None);
    }

    [Fact]
    public async Task IdentitySequenceAdvancer_OnSqlite_LeavesTheRowidGeneratorAlone()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var advancer = new IdentitySequenceAdvancer(
            new GeneratedKeyProperty(), new NullLogger<IdentitySequenceAdvancer>());

        var act = () => advancer.AdvanceAsync(
            db, db.Model.FindEntityType(typeof(RunEvent))!, 500, CancellationToken.None);

        await act.Should().NotThrowAsync("SQLite already takes the maximum rowid plus one");
    }

    [Fact]
    public async Task EntityTypeSet_ReachesATableByItsEntityType()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        var type = db.Model.FindEntityType(typeof(RunArtifact))!;
        var sets = new EntityTypeSet();

        (await sets.AnyAsync(db, type, CancellationToken.None)).Should().BeFalse();
        db.RunArtifacts.Add(new RunArtifact { Id = 3, RunId = "r", Kind = "k" });
        await db.SaveChangesAsync();

        (await sets.CountAsync(db, type, CancellationToken.None)).Should().Be(1);
        (await sets.AnyAsync(db, type, CancellationToken.None)).Should().BeTrue();
        var rows = new List<object>();
        await foreach (var row in sets.Rows(db, type)) rows.Add(row);
        rows.Should().ContainSingle().Which.Should().BeOfType<RunArtifact>();
    }

    [Fact]
    public async Task SuspendAuditStamping_WhenTheScopeEnds_TheStampingIsBackOn()
    {
        using var db = MigratedStoreTemplate.Context(_store);
        using (db.SuspendAuditStamping())
        {
            db.RunArtifacts.Add(new RunArtifact
            {
                Id = 1, RunId = "r", Kind = "k", CreatedAt = FullStoreSeed.SeededAt,
                UpdatedAt = FullStoreSeed.SeededAt,
            });
            await db.SaveChangesAsync();
        }

        db.RunArtifacts.Add(new RunArtifact { Id = 2, RunId = "r", Kind = "k" });
        await db.SaveChangesAsync();

        (await db.RunArtifacts.SingleAsync(a => a.Id == 1)).CreatedAt.Should().Be(FullStoreSeed.SeededAt);
        (await db.RunArtifacts.SingleAsync(a => a.Id == 2)).CreatedAt
            .Should().BeAfter(FullStoreSeed.SeededAt, "the suspension lasts exactly as long as its scope");
    }

    private static IReadOnlyList<string> Migrations(PersistenceProvider provider)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = provider == PersistenceProvider.Sqlite
                ? "Data Source=:memory:"
                : "Server=localhost;Database=x;TrustServerCertificate=True",
        });
        using var db = new AgentSmithDbContext(builder.Options);
        return [.. db.Database.GetMigrations().OrderBy(id => id, StringComparer.Ordinal)];
    }

    public void Dispose() => _store.Dispose();
}
