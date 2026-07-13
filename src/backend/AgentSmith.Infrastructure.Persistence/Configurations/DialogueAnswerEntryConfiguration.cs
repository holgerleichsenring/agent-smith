using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0327: the durable answer inbox. UNIQUE(DialogueJobId, QuestionId) IS the
/// first-answer-wins rule — a duplicate insert loses by construction.
/// </summary>
public sealed class DialogueAnswerEntryConfiguration : IEntityTypeConfiguration<DialogueAnswerEntry>
{
    public void Configure(EntityTypeBuilder<DialogueAnswerEntry> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.DialogueJobId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.Property(a => a.QuestionId).HasMaxLength(PersistenceLimits.IndexedString);
        builder.HasIndex(a => new { a.DialogueJobId, a.QuestionId }).IsUnique();
    }
}
