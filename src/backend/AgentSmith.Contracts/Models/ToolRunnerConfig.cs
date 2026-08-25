namespace AgentSmith.Contracts.Models;

/// <summary>
/// Configuration for the tool runner. Loaded from agentsmith.yml tool_runner section.
/// </summary>
public sealed class ToolRunnerConfig
{
    public string Type { get; set; } = "auto";
    public string? Socket { get; set; }
    public string? Namespace { get; set; }
    public string? ImagePullPolicy { get; set; }
    /// <summary>
    /// Scanner images the tool runner starts, overridable per key from agentsmith.yml.
    /// <para>
    /// 2026-08-25-014d: these sit OUTSIDE <c>sandbox.allowed_registries</c>, on purpose.
    /// That boundary exists because a sandbox toolchain image is named at run time by a
    /// model or by a catalog profile — an untrusted author. These are named here, in the
    /// binary, by this repository, and they are not sandboxes: no repository is cloned
    /// into them and no agent runs there. Holding them to the sandbox policy would refuse
    /// the shipped defaults on a stock installation without making anything safer.
    /// An operator who repoints a key repoints it deliberately, in their own file.
    /// A test pins this set, so a fourth compiled-in image has to be classified rather
    /// than added quietly.
    /// </para>
    /// </summary>
    public Dictionary<string, string> Images { get; set; } = new()
    {
        ["nuclei"] = "projectdiscovery/nuclei:latest",
        ["spectral"] = "stoplight/spectral:6",
    };

    /// <summary>
    /// Hostname used to reach the host from inside a container.
    /// Defaults to "host.docker.internal" (Docker Desktop / --add-host on Linux).
    /// Set to a different value for Podman or custom networking.
    /// </summary>
    public string DockerHostname { get; set; } = "host.docker.internal";
}
