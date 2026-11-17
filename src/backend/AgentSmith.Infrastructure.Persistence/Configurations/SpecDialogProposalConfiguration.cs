using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-11-17-a0b6d: a conversation's proposals, keyed by run id (unique), read by session id and decided by proposal id.
/// The JSON is declared by nothing — unbounded, each provider's own convention picks the type.
/// <para>
/// AT MOST ONE AWAITING ROW PER SESSION: a unique index over SessionId filtered to the awaiting state. Two replicas that
/// each put a proposal up write two different rows, so a compare-and-set on one row's state cannot see the other; the
/// index refuses the second. The filter is SQL, quoted per provider; MySQL has no partial index and gets none.
/// </para>
/// </summary>
public sealed class SpecDialogProposalConfiguration(string? providerName)
{
    public const string OneAwaitingIndex = "IX_SpecDialogProposal_OneAwaiting";

    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string MySqlProvider = "Pomelo.EntityFrameworkCore.MySql";

    public void Apply(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SpecDialogProposal>();
        builder.HasKey(p => p.Id);
        builder.Property(p => p.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.RunId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(p => p.ProposalId).HasMaxLength(PersistenceLimits.ProposalId);
        builder.Property(p => p.State).HasMaxLength(PersistenceLimits.ProposalId);
        builder.HasIndex(p => p.RunId).IsUnique();
        builder.HasIndex(p => p.SessionId);
        builder.HasIndex(p => p.ProposalId);
        if (providerName == MySqlProvider) return;
        builder.HasIndex(p => p.SessionId, OneAwaitingIndex).IsUnique().HasFilter(AwaitingFilter(providerName));
    }

    /// <summary>The filter of the one-awaiting index, as the provider spells it.</summary>
    public static string AwaitingFilter(string? providerName) =>
        providerName == SqlServerProvider
            ? $"[State] = '{SpecDialogProposalClaim.Awaiting}'"
            : $"\"State\" = '{SpecDialogProposalClaim.Awaiting}'";
}
