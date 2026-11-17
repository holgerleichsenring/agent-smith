using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-16-f3fbd: adds one registry prefix to the stored <c>sandbox.allowed_registries</c>, attributed
/// to the run that asked. The run's own step calls it, so the write has a run id; the server writes the
/// settings store, an installation without one refuses with a sentence naming the setting.
/// </summary>
public interface ISandboxRegistryTrustWriter
{
    /// <summary>Trusts <paramref name="prefix"/>, once; an entry already listed is not written again.</summary>
    Task<RegistryTrustWrite> TrustAsync(string prefix, string runId, CancellationToken cancellationToken);
}
