using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-08-25-0d01: reports every project whose sandbox-agent version was PINNED away from
/// the release this server is. Deriving the version removed the accidental mismatch; this
/// is the other half of the ruling — the deliberate one is judged and seen.
/// <para>
/// Advisory, never blocking, and deliberately silent about compatibility. The pin may be
/// exactly right (an air-gapped mirror carries one tag, a developer is bisecting), and a
/// tag is not evidence about a protocol. What actually decides whether the two can talk is
/// the version the agent stamps on what it sends back, which
/// <see cref="Sandbox.WireProtocolWatcher"/> reads off the live channel.
/// </para>
/// </summary>
public sealed class PinnedAgentProbe(AgentSmithConfig config, IAgentVersionResolver versions)
    : IStartupProbe
{
    public string Subsystem => StartupSubsystems.SandboxAgent;

    public Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StartupFinding>>(
            [.. config.Projects.Select(p => Judge(p.Key, p.Value)).OfType<StartupFinding>()]);

    private StartupFinding? Judge(string name, ResolvedProject project)
    {
        var choice = Choose(project);
        if (choice?.DiffersFromServer != true) return null;
        // The in-process backend registers a placeholder so a reference can be FORMED where
        // no image is ever pulled. Judging it as a release would report a skew that has no
        // image on either side of it.
        if (choice.Version == AgentImageDefaults.InProcessVersion) return null;
        return new StartupFinding(
            StartupSubsystems.SandboxAgent, StartupFindingSeverity.Advisory,
            Reason(name, choice), Project: name, Field: "sandbox.agent_version");
    }

    // A project whose version can be resolved neither way is a configuration fault the
    // configuration probe already reports; this one has nothing to add about it.
    private AgentVersionChoice? Choose(ResolvedProject project)
    {
        try { return versions.Resolve(project); }
        catch (InvalidOperationException) { return null; }
    }

    // The pin reaches the sandbox from either 'sandbox.agent_version' or the one-knob
    // 'deployment.version', which fills it when it is unset — and that same knob pins the
    // ORCHESTRATOR image, which has no derived default. So the way out is named precisely
    // rather than as "delete it", which would take the orchestrator's pin with it.
    private static string Reason(string project, AgentVersionChoice choice) =>
        $"Project '{project}' pins its sandbox agent to {choice.Version}, while this server is "
        + $"release {choice.ServerVersion}. The pin comes from 'sandbox.agent_version' or from "
        + "'deployment.version', which fills it; leave both unset and the agent tag follows this "
        + "server instead — but 'deployment.version' also pins the orchestrator image, which "
        + "still needs one of its own. Nothing has been refused over the difference, and whether "
        + "the pinned agent can still talk to this server is reported separately, from what it "
        + "actually answers.";
}
