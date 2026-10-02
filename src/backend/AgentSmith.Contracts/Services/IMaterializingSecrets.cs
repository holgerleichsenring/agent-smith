namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89a: the secrets of the configuration being materialized on THIS flow. Catalog
/// resolution can discover a connection's repos, and discovery reads the connection's token; the
/// configuration that token belongs to is the one being built, which no ISecretValues knows yet
/// — the server's even reloads through the very loader running. While a materialization is in
/// flight, a credential is looked up here first.
/// </summary>
public interface IMaterializingSecrets
{
    /// <summary>The in-flight configuration's secrets, or null outside a materialization.</summary>
    ISecretValues? Current { get; }

    /// <summary>Makes <paramref name="secrets"/> current on this flow until disposed.</summary>
    IDisposable Begin(IReadOnlyDictionary<string, string> secrets);
}
