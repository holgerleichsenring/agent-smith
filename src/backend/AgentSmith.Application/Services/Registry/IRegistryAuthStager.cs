using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Bounded, read-only LLM fallback that stages private-registry auth for an
/// ecosystem the deterministic fast-paths do not cover. It inspects the repo's
/// config/manifest files and emits the GLOBAL auth-config file(s) that manager
/// reads, using <c>__AS_TOKEN_&lt;host&gt;__</c> placeholders — the real token is
/// NEVER given to the model and never appears in the prompt, response, or history.
/// </summary>
public interface IRegistryAuthStager
{
    Task<RegistryAuthStagingResult> StageAsync(
        ISandbox sandbox, string repoRoot,
        IReadOnlyList<UncoveredRegistry> uncovered, AgentConfig agent,
        CancellationToken cancellationToken);
}
