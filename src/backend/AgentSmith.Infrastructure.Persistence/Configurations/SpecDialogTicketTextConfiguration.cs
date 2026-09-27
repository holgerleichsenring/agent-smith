using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-09-25-8e51c: one ticket text per design conversation. The session id is unique here — a
/// conversation reads its ticket once and re-reads in place, so a second row would be a second
/// answer to "what does this conversation think the ticket says".
/// <para>
/// The text is declared by LENGTH and by nothing else, for the reason the subject column gives:
/// this migration set is generated from SQLite and three providers run it verbatim, so a literal
/// column type would be provider-blind.
/// </para>
/// </summary>
public sealed class SpecDialogTicketTextConfiguration : IEntityTypeConfiguration<SpecDialogTicketText>
{
    public void Configure(EntityTypeBuilder<SpecDialogTicketText> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.SessionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.TicketId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.Title).HasMaxLength(PersistenceLimits.ConversationSubject);
        builder.Property(t => t.Fingerprint).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(t => t.Text).HasMaxLength(PersistenceLimits.SeededTicketText);
        builder.HasIndex(t => t.SessionId).IsUnique();
    }
}
