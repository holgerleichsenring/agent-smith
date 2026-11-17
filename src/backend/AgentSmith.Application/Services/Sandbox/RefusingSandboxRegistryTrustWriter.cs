using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-16-f3fbd: the writer of an installation that has no settings store a run can write — the CLI
/// reads agentsmith.yml and never writes it back. It refuses, naming the setting that lifts the refusal.
/// </summary>
public sealed class RefusingSandboxRegistryTrustWriter : ISandboxRegistryTrustWriter
{
    public Task<RegistryTrustWrite> TrustAsync(string prefix, string runId, CancellationToken cancellationToken) =>
        Task.FromResult(RegistryTrustWrite.Refused(
            $"sandbox.allow_new_registries is on, but this installation keeps its configuration in a file a run "
            + $"does not write: add '{prefix}' to sandbox.allowed_registries in agentsmith.yml"));
}
