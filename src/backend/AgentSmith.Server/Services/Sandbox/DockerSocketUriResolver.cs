using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0391b/p0407: turns the configured DOCKER_HOST into a URI. A value that is not a
/// URI is an operator typo, not a reason to refuse to start — the default socket is
/// used instead and a startup finding names the variable, so the Docker backend
/// simply fails to reach it, which is a state the server can describe.
/// </summary>
public sealed class DockerSocketUriResolver(IStartupFindings findings)
{
    public const string DefaultSocket = "unix:///var/run/docker.sock";

    public Uri Resolve(string configured)
    {
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri)) return uri;

        findings.Record(new StartupFinding(
            StartupSubsystems.Spawner,
            StartupFindingSeverity.Blocking,
            $"DOCKER_HOST '{configured}' is not a valid URI, so the Docker sandbox backend "
            + $"falls back to '{DefaultSocket}'. Expected form: unix:///var/run/docker.sock "
            + "or tcp://host:port.",
            Field: "DOCKER_HOST"));
        return new Uri(DefaultSocket);
    }
}
