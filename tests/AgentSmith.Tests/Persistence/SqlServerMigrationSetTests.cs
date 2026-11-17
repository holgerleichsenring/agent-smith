using System.Text.RegularExpressions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-17-a0b6d: the SQL Server set, checked offline as the PostgreSQL one is — its snapshot describes the model, and
/// the session row it describes holds no proposal and no proposal state.
/// </summary>
public sealed class SqlServerMigrationSetTests
{
    private const string Assembly = "AgentSmith.Infrastructure.Persistence.SqlServer";

    [Fact]
    public void SqlServerMigrationSet_HasNoPendingModelChanges()
    {
        using var db = Context();

        db.Database.HasPendingModelChanges().Should().BeFalse("every model change needs a migration in the SQL Server set too");
    }

    [Fact]
    public void SqlServerSnapshot_SessionHoldsNoProposal()
    {
        var snapshot = File.ReadAllText(Path.Combine(
            ArchitectureSources.BackendRoot, Assembly, "Migrations", "AgentSmithDbContextModelSnapshot.cs"));
        var session = Regex.Match(snapshot,
            @"Entity\(""AgentSmith\.Infrastructure\.Persistence\.Entities\.SpecDialogSession"",.*?\n                \}\);",
            RegexOptions.Singleline).Value;

        session.Should().Contain("\"PostedProposalId\"", "the block found is the session's");
        foreach (var column in new[] { "LatestProposalJson", "AwaitingProposalJson", "ReviewingProposalJson", "ConfirmedOutcomeJson",
                     "ConfirmedProposalId", "\"ProposalId\"", "ProposalState" })
            session.Should().NotContain(column);
        snapshot.Should().Contain("[State] = 'awaiting'", "one proposal awaits per session");
    }

    private static AgentSmithDbContext Context()
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = PersistenceProvider.SqlServer,
            ConnectionString = "Server=localhost;Database=x;User Id=sa;Password=x;TrustServerCertificate=True",
        });
        return new AgentSmithDbContext(builder.Options);
    }
}
