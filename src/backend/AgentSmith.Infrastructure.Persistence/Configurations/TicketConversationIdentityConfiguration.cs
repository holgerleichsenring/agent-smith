using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-25-8e51b: one design conversation per ticket. The artifact a ticket conversation
/// amends — the approval record — is keyed by ticket and upserts in place, so two conversations
/// about one ticket would be two drafts of one thing with no merge and a silent last-writer-wins.
/// <para>
/// A conversation that belongs to NO ticket carries nulls in both columns, and SQL Server is the
/// one provider that treats two NULLs as equal in a unique index — an unfiltered index there
/// would permit exactly one ticket-less conversation in the whole table. So the index is filtered
/// to the rows that HAVE a ticket, on that provider only, and the provider is asked by name so
/// neither model snapshot has to be told a lie about the other's. Same shape, and the same
/// reason, as <see cref="RunRecordIdentityConfiguration"/>.
/// </para>
/// </summary>
public sealed class TicketConversationIdentityConfiguration(string? providerName)
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string SqlServerFilter = "[Tracker] IS NOT NULL AND [TicketKey] IS NOT NULL";

    public void Apply(ModelBuilder modelBuilder)
    {
        var index = modelBuilder.Entity<SpecDialogSession>()
            .HasIndex(nameof(SpecDialogSession.Tracker), nameof(SpecDialogSession.TicketKey))
            .IsUnique();
        if (providerName == SqlServerProvider) index.HasFilter(SqlServerFilter);
    }
}
