using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-17-f3a8e: unwrapKey against Azure Key Vault. The server authenticates as itself — workload identity,
/// managed identity (AZURE_CLIENT_ID picks a user-assigned one) or a service principal from the environment — never
/// through a developer's or an interactive sign-in. Retries and the network timeout are short, because the unwrap
/// runs while the configuration loads: a vault that cannot answer makes the stored values unreadable, said as a
/// finding, instead of holding the load for minutes.
/// </summary>
public sealed class AzureKeyVaultUnwrap(Func<Uri, CryptographyClient>? clients = null) : IKeyVaultUnwrap
{
    // not a limit: the SDK's transport settings for one call made before the configuration exists — it is what
    // opens the stored values the configuration resolves — so they cannot live in that configuration.
    private const int MaxRetries = 2; // not a limit: see above
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(10); // not a limit: see above

    private readonly Lazy<TokenCredential> _credential = new(Credential);

    public byte[] Unwrap(Uri keyId, byte[] wrapped)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        var client = clients?.Invoke(keyId) ?? new CryptographyClient(keyId, _credential.Value, Options());
        return client.UnwrapKey(KeyWrapAlgorithm.RsaOaep256, wrapped).Key;
    }

    internal static CryptographyClientOptions Options()
    {
        var options = new CryptographyClientOptions();
        options.Retry.MaxRetries = MaxRetries;
        options.Retry.NetworkTimeout = NetworkTimeout;
        return options;
    }

    internal static DefaultAzureCredentialOptions CredentialOptions() => new()
    {
        ExcludeInteractiveBrowserCredential = true,
        ExcludeSharedTokenCacheCredential = true,
        ExcludeVisualStudioCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeAzureCliCredential = true,
        ExcludeAzurePowerShellCredential = true,
        ExcludeAzureDeveloperCliCredential = true,
    };

    private static TokenCredential Credential() => new DefaultAzureCredential(CredentialOptions());
}
