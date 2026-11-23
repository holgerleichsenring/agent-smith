using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>
/// 2026-11-23-933de: Azure Container Registry. Images are &lt;login_server&gt;/&lt;repository&gt;/&lt;poc&gt;; the token
/// realm ACR names is https://&lt;login_server&gt;/oauth2/token, which the shared realm rule admits. The login-server
/// shape (name, optional domain-name-label hash, sovereign clouds) is checked by <see cref="PocRegistryConfig"/>.
/// </summary>
public sealed class AcrContainerRegistry(PocRegistryConfig config, OciRegistryReader reader) : IContainerRegistry
{
    public Task<RegistryProbe> ProbeAsync(CancellationToken cancellationToken) => reader.ProbeAsync(cancellationToken);

    public ImageReference Reference(string poc, string tag) =>
        new(config.LoginServer, $"{config.Repository}/{poc}", tag);

    public Task<string?> ResolveAsync(ImageReference reference, CancellationToken cancellationToken) =>
        reader.ResolveAsync(reference, cancellationToken);

    public Task<bool> ExistsAsync(ImageReference reference, CancellationToken cancellationToken) =>
        reader.ExistsAsync(reference, cancellationToken);
}
