using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// Spec-dialog sessions (p0315a). SessionId is the unique resume handle; the
/// (Platform, ThreadId) index serves the per-thread open-session lookup on
/// every inbound chat message, and the owner index serves the caller's conversation list.
/// Indexed strings cap at the MySQL-safe length.
/// </summary>
public sealed class SpecDialogSessionConfiguration : IEntityTypeConfiguration<SpecDialogSession>
{
    public void Configure(EntityTypeBuilder<SpecDialogSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.Platform).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.ThreadId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.ChannelId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.UserId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.Project).HasMaxLength(PersistenceLimits.IndexedString);
        // 2026-09-20-4b0af: declared by LENGTH and by nothing else. This project's migrations
        // are run by Sqlite, Postgres and MySQL alike, so a literal column type here would be
        // provider-blind; a bounded string lets each provider's own convention pick the type.
        builder.Property(s => s.Subject).HasMaxLength(PersistenceLimits.ConversationSubject);
        // 2026-09-25-8e51b: the ticket a conversation belongs to. The UNIQUE index over the pair
        // lives on the context, not here — it has to be filtered on SQL Server, which treats two
        // NULLs as equal, and an entity configuration cannot see the provider name.
        builder.Property(s => s.Tracker).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(s => s.TicketKey).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(s => s.SessionId).IsUnique();
        builder.HasIndex(s => new { s.Platform, s.ThreadId });
        builder.HasIndex(s => s.UserId);
    }
}
