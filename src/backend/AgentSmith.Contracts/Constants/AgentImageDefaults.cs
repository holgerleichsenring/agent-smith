namespace AgentSmith.Contracts.Constants;

/// <summary>
/// Build-time constants identifying the agent's carrier image. Registry
/// (where to pull from) and version (which tag) are operator-configurable
/// via agentsmith.yml — see <c>SandboxGlobalConfig</c> for global defaults
/// and <c>SandboxConfig</c> for per-project overrides. The image *name*
/// is the project's published identity and lives here as a single constant.
/// </summary>
public static class AgentImageDefaults
{
    /// <summary>Image name (sans registry, sans tag) of the sandbox carrier image published by this project's CI.</summary>
    public const string SandboxAgentImageName = "agent-smith-sandbox-agent";

    /// <summary>Image name (sans registry, sans tag) of the orchestrator container the dispatcher spawns to run a pipeline. Distinct from the sandbox carrier — the orchestrator hosts the pipeline-runner process, the sandbox carrier hosts the agent runtime inside the per-language toolchain.</summary>
    public const string OrchestratorImageName = "agentsmith-cli";

    /// <summary>Default container registry for the sandbox agent image when neither agentsmith.yml nor per-project config specifies one.</summary>
    public const string DefaultRegistry = "holgerleichsenring";
}
