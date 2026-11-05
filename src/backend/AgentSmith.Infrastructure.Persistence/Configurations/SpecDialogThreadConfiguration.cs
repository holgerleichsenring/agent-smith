using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// The thread a design conversation's last turn ended with. The session index is NOT unique: a replace adds before
/// it deletes, so two rows stand for a moment. The messages are declared by nothing at all — unbounded, each
/// provider's own convention picks the type, as the transcript's column does.
/// </summary>
public sealed class SpecDialogThreadConfiguration : IEntityTypeConfiguration<SpecDialogThread>
{
    public void Configure(EntityTypeBuilder<SpecDialogThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(t => t.SessionId);
    }
}
