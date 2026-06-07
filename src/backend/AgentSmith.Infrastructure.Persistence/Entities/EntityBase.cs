namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// Audit base for every persisted entity. CreatedAt is stamped on insert,
/// UpdatedAt on every save (AgentSmithDbContext.SaveChanges sets both), so the
/// trail carries when each row was written without the callers threading clocks.
/// </summary>
public abstract class EntityBase
{
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
