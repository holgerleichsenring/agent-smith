using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// The model adjustments that depend on the provider, asked by name so no other provider's
/// snapshot changes. Applied after every entity configuration, because they refine columns and
/// indexes those declared.
/// </summary>
public sealed class ProviderConfigurations(string? providerName)
{
    public void Apply(ModelBuilder modelBuilder)
    {
        new RunRecordIdentityConfiguration(providerName).Apply(modelBuilder); // 61f1
        new TicketConversationIdentityConfiguration(providerName).Apply(modelBuilder); // 8e51b
        new MoneyPrecisionConfiguration(providerName).Apply(modelBuilder); // 2026-08-28-b883
        // LAST: covers every DateTimeOffset column the configurations above declared.
        new UtcTimestampConfiguration(providerName).Apply(modelBuilder); // 2026-10-28-ebb0
    }
}
