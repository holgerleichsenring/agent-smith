using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992d: a workspace whose project scopes secrets to repositories (<c>sandbox.secrets.repo_env</c>) needs
/// agents that read <see cref="Step.SecretScope"/>. An older agent reads the step and ignores the scope, so its
/// commands would silently go without their repository's credentials. Each container is sent one harmless step — a
/// listing of "/" at depth 0, which every agent answers — and the protocol stamped on its answer decides.
/// </summary>
public sealed class WorkspaceAgentProtocolGate
{
    public const int ScopedSecretsProtocol = 3; // not a limit: the wire protocol that introduced Step.SecretScope

    /// <summary>Throws, naming the agent image, the container's toolchain and the protocol its agent speaks, when an
    /// agent is below <see cref="ScopedSecretsProtocol"/>. Nothing is asked when no secret is scoped.</summary>
    public async Task RefuseOldAgentsAsync(
        SandboxSpec spec, IReadOnlyList<(string Image, ISandbox Container)> containers, CancellationToken ct)
    {
        if (spec.Secrets is not { RepoEnv.Count: > 0 }) return;
        foreach (var (image, container) in containers)
        {
            var answer = await container.RunStepAsync(Probe(), null, ct);
            if (answer.SchemaVersion >= ScopedSecretsProtocol) continue;
            throw new InvalidOperationException(
                $"The sandbox agent from agent image '{spec.AgentImage}' (toolchain '{image}') speaks wire protocol "
                + $"{answer.SchemaVersion}; sandbox.secrets.repo_env needs protocol {ScopedSecretsProtocol}. Use an agent "
                + "image of this release, or remove the project's sandbox.secrets.repo_env.");
        }
    }

    private static Step Probe() => new(
        Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
        Path: "/", MaxDepth: 0);
}
