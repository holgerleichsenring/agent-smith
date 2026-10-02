using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-01-283da: the tables holding what an operator handed a design conversation — the
/// legacy image table, kept until every replica reads the new one, and the reference files.
/// Lifted out of the context, which is at its length ceiling.
/// </summary>
public sealed class DialogFileConfigurations(string? providerName)
{
    public void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new SpecDialogAttachmentConfiguration()); // 3af8
        modelBuilder.ApplyConfiguration(new ReferenceFileConfiguration(providerName)); // 2026-10-01-283da
    }
}
