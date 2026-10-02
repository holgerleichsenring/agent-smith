using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Configurations;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-01-283da: the column a reference file's bytes live in, and where its identity starts.
/// The shared migration set emits literals three providers run verbatim, so the content column is
/// untyped there and each provider maps a byte array to its own large binary type; the identity
/// starts at a billion so a copied legacy image keeps its number.
/// </summary>
public sealed class ReferenceFileColumnTests
{
    private const string MigrationName = "_AddReferenceFiles.cs";

    [Theory]
    [InlineData(PersistenceProvider.Sqlite, "BLOB")]
    [InlineData(PersistenceProvider.Postgresql, "bytea")]
    [InlineData(PersistenceProvider.Mysql, "longblob")]
    [InlineData(PersistenceProvider.SqlServer, "varbinary(max)")]
    public void ReferenceFile_ContentColumn_MapsToEachProvidersBinaryType(
        PersistenceProvider provider, string expected)
    {
        using var context = For(provider);

        var property = context.Model.FindEntityType(typeof(ReferenceFile))!
            .FindProperty(nameof(ReferenceFile.Content))!;

        property.GetMaxLength().Should().BeNull("a bound is what makes a provider reach for a small type");
        property.GetRelationalTypeMapping().StoreType.Should().Be(expected);
    }

    [Theory]
    [InlineData("AgentSmith.Infrastructure.Persistence", "INSERT INTO sqlite_sequence (name, seq) VALUES ('ReferenceFiles', 999999999);")]
    [InlineData("AgentSmith.Infrastructure.Persistence.SqlServer", "\"SqlServer:Identity\", \"1000000000, 1\"")]
    public void ReferenceFile_BothMigrationSets_DeclareNoTypeForContentAndSeedTheIdentity(
        string assembly, string seed)
    {
        var migration = File.ReadAllText(Directory.GetFiles(
            Path.Combine(ArchitectureSources.BackendRoot, assembly, "Migrations"), "*" + MigrationName).Single());

        migration.Should().Contain(seed, "the identity starts above every legacy image id");
        if (assembly == "AgentSmith.Infrastructure.Persistence")
            migration.Split('\n').Single(line => line.Contains("Content = table.Column"))
                .Should().NotContain("type:", "three providers run this set verbatim");
    }

    [Fact]
    public void DialogFileConfigurations_Apply_MapsTheLegacyAndTheReferenceTable()
    {
        using var context = For(PersistenceProvider.Sqlite);

        context.Model.FindEntityType(typeof(SpecDialogAttachment))!.GetTableName().Should().Be("SpecDialogAttachments");
        context.Model.FindEntityType(typeof(ReferenceFile))!.GetTableName().Should().Be("ReferenceFiles");
        new DialogFileConfigurations(null).Invoking(c => c.Apply(null!)).Should().Throw<ArgumentNullException>();
    }

    private static AgentSmithDbContext For(PersistenceProvider provider)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions { Provider = provider, ConnectionString = Placeholder(provider) });
        return new AgentSmithDbContext(builder.Options);
    }

    // The mapping is model metadata: none of these ever opens the connection.
    private static string Placeholder(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.Sqlite => "Data Source=:memory:",
        PersistenceProvider.Postgresql => "Host=localhost;Database=agentsmith",
        PersistenceProvider.Mysql => "Server=localhost;Database=agentsmith",
        _ => "Server=localhost;Database=agentsmith;TrustServerCertificate=True",
    };
}
