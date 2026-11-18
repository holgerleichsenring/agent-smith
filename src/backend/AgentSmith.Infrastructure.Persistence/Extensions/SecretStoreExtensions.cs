using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>2026-11-17-f3a8d: the installation's secret store — the master keys from the environment (2026-11-17-f3a8e:
/// raw, or wrapped and unwrapped by Azure Key Vault), the envelope, the write-only vault and the values a configuration
/// load opens.</summary>
public static class SecretStoreExtensions
{
    public static IServiceCollection AddSecretStore(this IServiceCollection services)
    {
        // 2026-11-17-f3a8e: a wrapped master key is unwrapped by Azure Key Vault when the ring is first asked for.
        services.AddSingleton(_ => new MasterKeyRing(Environment.GetEnvironmentVariable, new AzureKeyVaultUnwrap()));
        services.AddSingleton<SecretEnvelope>().TryAddSingleton(TimeProvider.System);
        services.AddScoped<StoredSecretRepository>().AddScoped<ISecretVault>(sp => sp.GetRequiredService<StoredSecretRepository>());
        services.AddSingleton<IStoredSecretValues, StoredSecretValues>();
        return services;
    }
}
