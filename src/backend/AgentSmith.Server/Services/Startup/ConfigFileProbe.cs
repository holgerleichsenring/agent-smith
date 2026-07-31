using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: the bootstrap file that names the database and the secret env-vars. Missing,
/// the server falls back to the built-in defaults — which is a legitimate state for a
/// local run and a serious one in a container, so it is a finding, not an exit code.
/// This check used to be Environment.Exit(78) before the host even existed.
/// </summary>
public sealed class ConfigFileProbe(ServerContext context) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.ConfigFile;

    public Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StartupFinding>>(
            File.Exists(context.ConfigPath) ? [] : [Missing(context.ConfigPath)]);

    private static StartupFinding Missing(string configPath) => new(
        StartupSubsystems.ConfigFile,
        StartupFindingSeverity.Advisory,
        $"No bootstrap config at '{configPath}', so the built-in defaults are in use — the "
        + "database connection and the secret env-var names come from that file. Mount it at "
        + "/app/config/agentsmith.yml (k8s: the ConfigMap volume; docker: -v) or set CONFIG_PATH.",
        Field: "CONFIG_PATH");
}
