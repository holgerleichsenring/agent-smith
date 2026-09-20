using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-09-20-3af8: the column an operator's screenshot is stored in.
/// <para>
/// The shared migration set is generated from SQLite and emits its column types as LITERALS,
/// which three providers then run verbatim. "TEXT" means sixty-four kilobytes on MySQL — about
/// a hundredth of an encoded five-megabyte image, an error in strict mode and a SILENT
/// truncation otherwise. A per-provider literal is no fix (one literal is provider-blind and
/// LONGTEXT is not a Postgres type) and a conditional one is no fix either (the shared assembly
/// holds ONE snapshot). The column declares no type at all, and each provider's own convention
/// maps it — which is what this asserts, on the migration AND on the mapping.
/// </para>
/// </summary>
public sealed class DialogAttachmentColumnTests
{
    private const string ContentColumn = nameof(SpecDialogAttachment.ContentBase64);

    /// <summary>What five megabytes of image becomes once it is base64 text.</summary>
    private const int EncodedImageChars = 5 * 1024 * 1024 * 4 / 3;

    [Theory]
    [InlineData(PersistenceProvider.Sqlite, "TEXT")]
    [InlineData(PersistenceProvider.Postgresql, "text")]
    [InlineData(PersistenceProvider.Mysql, "longtext")]
    [InlineData(PersistenceProvider.SqlServer, "nvarchar(max)")]
    public void Dialog_TheAttachmentColumn_MapsToEachProvidersLargeTextType(
        PersistenceProvider provider, string expected)
    {
        using var context = For(provider);

        var property = context.Model.FindEntityType(typeof(SpecDialogAttachment))!
            .FindProperty(ContentColumn)!;
        var mapping = property.GetRelationalTypeMapping();

        property.GetMaxLength().Should().BeNull(
            "a declared length is what makes a provider reach for a bounded type; SQLite hides "
            + "that by storing every string as TEXT, so the bound is asserted where it is set");
        mapping.StoreType.Should().Be(expected);
        (mapping.Size ?? int.MaxValue).Should().BeGreaterThan(EncodedImageChars,
            "an encoded five-megabyte image has to fit, and sixty-four kilobytes is where "
            + "a scaffolded literal would have put it on MySQL");
    }

    [Fact]
    public void Dialog_TheSharedMigration_DeclaresNoTypeForTheAttachmentColumn()
    {
        var migration = File.ReadAllLines(Path.Combine(
            ArchitectureSources.BackendRoot, "AgentSmith.Infrastructure.Persistence", "Migrations",
            "20260920105644_AddSpecDialogAttachments.cs"));

        var column = migration.Single(line => line.Contains($"{ContentColumn} = table.Column"));

        column.Should().NotContain("type:",
            "three providers run this set verbatim, so a literal here is the wrong word on two "
            + "of them — untyped is what lets each provider's convention map it");
    }

    private static AgentSmithDbContext For(PersistenceProvider provider)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        AgentSmith.Infrastructure.Persistence.Extensions.PersistenceOptionsExtensions.UseProvider(
            builder,
            new PersistenceOptions { Provider = provider, ConnectionString = Placeholder(provider) });
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
