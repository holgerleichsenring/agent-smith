namespace AgentSmith.Infrastructure.Persistence.Services.Secrets;

/// <summary>
/// 2026-11-17-f3a8e: the one thing the secret store asks of Azure Key Vault — decrypt (unwrapKey, RSA-OAEP-256) the
/// master key the deployment delivers wrapped, with the vault key the deployment names. The private half never
/// leaves the vault.
/// </summary>
public interface IKeyVaultUnwrap
{
    byte[] Unwrap(Uri keyId, byte[] wrapped);
}
