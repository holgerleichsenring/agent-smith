using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-28-b630: asks a sandbox which of the credentials projected into it did not
/// arrive, and gets back NAMES.
/// <para>
/// Presence is proven where the value arrives, and Kubernetes already proved half of it: a
/// pod whose secret reference cannot be resolved never starts, so a sandbox that came up
/// carries the proof for its environment. What remains is that the variable is set and
/// non-empty and the file is there — a test that reads a name and a length, never a value.
/// </para>
/// </summary>
public interface ISandboxSecretPresenceProbe
{
    /// <summary>The env names and mount paths that failed the test, in the order declared.</summary>
    Task<IReadOnlyList<string>> MissingAsync(
        ISandbox sandbox, ResolvedSandboxSecrets secrets, CancellationToken cancellationToken);
}
