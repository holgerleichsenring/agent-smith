using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0432: a copy must carry the current schema and must not be a share.
/// </summary>
public sealed class MigratedStoreTemplateTests
{
    [Fact]
    public void AStoreFromTheTemplate_CarriesTheCurrentSchema()
    {
        using var connection = MigratedStoreTemplate.OpenCopy();
        using var ctx = MigratedStoreTemplate.Context(connection);

        ctx.Database.GetAppliedMigrations().Should().NotBeEmpty(
            "the copy carries the migration history of the template it came from");
        ctx.Runs.Should().BeEmpty("a fresh store, not somebody else's");
    }

    [Fact]
    public void TwoStoresFromTheTemplate_DoNotSeeEachOthersWrites()
    {
        using var first = MigratedStoreTemplate.OpenCopy();
        using var second = MigratedStoreTemplate.OpenCopy();

        using (var ctx = MigratedStoreTemplate.Context(first))
        {
            ctx.Runs.Add(new Run { Id = "only-in-the-first", Project = "p", Pipeline = "code" });
            ctx.SaveChanges();
        }

        using var other = MigratedStoreTemplate.Context(second);
        other.Runs.Should().BeEmpty("a copy is not a share");
    }
}
