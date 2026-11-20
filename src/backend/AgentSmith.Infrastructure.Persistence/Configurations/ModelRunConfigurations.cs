using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>2026-11-20-24bd: the model places and the waits for them, applied together.</summary>
public sealed class ModelRunConfigurations
{
    public void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new ModelRunSlotConfiguration());
        modelBuilder.ApplyConfiguration(new ModelRunWaitConfiguration());
    }
}
