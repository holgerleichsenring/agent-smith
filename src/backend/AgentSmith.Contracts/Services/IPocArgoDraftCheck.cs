using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-23-c81ec: tests an unsaved PoC target argo block — its own values, its token from the secrets catalog and
/// that token's expiry, whether Argo CD takes it, and the target's ApplicationSet as Argo CD holds it now (what the last
/// install wrote). Nothing is saved or written.
/// </summary>
public interface IPocArgoDraftCheck
{
    Task<DraftCheckReport> RunAsync(PocArgoCheckRequest request, CancellationToken cancellationToken);
}
