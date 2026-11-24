using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-23-933de: tests an unsaved PoC target registry block — its own values, its pull token from the secrets
/// catalog, then whether that token may pull under the repository prefix. Nothing is saved.
/// </summary>
public interface IPocRegistryDraftCheck
{
    Task<DraftCheckReport> RunAsync(PocRegistryConfig draft, CancellationToken cancellationToken);
}
