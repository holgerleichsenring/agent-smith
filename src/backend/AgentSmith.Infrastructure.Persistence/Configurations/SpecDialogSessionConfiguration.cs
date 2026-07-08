using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// Spec-dialog sessions (p0315a). SessionId is the unique resume handle; the
/// (Platform, ThreadId) index serves the per-thread open-session lookup on
/// every inbound chat message. Indexed strings cap at the MySQL-safe length.
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
        builder.HasIndex(s => s.SessionId).IsUnique();
        builder.HasIndex(s => new { s.Platform, s.ThreadId });
    }
}
