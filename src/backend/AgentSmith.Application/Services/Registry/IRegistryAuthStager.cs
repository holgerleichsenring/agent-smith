using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// p0375: the bounded, Scout-role, read-only LLM fallback for registry
/// ecosystems the deterministic NuGet/npm fast-paths do not cover. Inspects the
/// matched repo files and emits the GLOBAL auth-config file(s) per uncovered
/// host with <c>__AS_TOKEN_&lt;host&gt;__</c> placeholders — never a real token
/// (the secret is substituted host-side after the call).
/// </summary>
public interface IRegistryAuthStager
{
    Task<RegistryAuthStagingResult> StageAsync(
        ISandbox sandbox, string repoRoot,
        IReadOnlyList<UncoveredRegistry> uncovered, AgentConfig agent,
        CancellationToken cancellationToken);
}
