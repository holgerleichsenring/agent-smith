using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: agentsmith.yml parses, passes cross-cutting validation, and every
/// ${SECRET} placeholder resolved to a non-empty value. The loader silently turns
/// an unset environment variable into an empty string — which then fails much later
/// as a cryptic 401 on a provider call; this check names the unset secret up front.
/// </summary>
public sealed class ConfigSchemaCheck(
    IPreflightConfigSource configSource,
    AgentSmithConfigValidator validator) : IPreflightCheck
{
    public string Name => "config-schema";

    public string Category => "config";

    public Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Evaluate());

    private PreflightCheckResult Evaluate()
    {
        var loaded = configSource.Resolve();
        if (loaded.Config is null)
            return PreflightCheckResult.Fail(
                $"agentsmith.yml failed to load from '{loaded.ConfigPath}': {loaded.LoadError}",
                "Fix the YAML or schema error above — every other check depends on a loadable config.");

        var config = loaded.Config;
        var errors = validator.Validate(config);
        if (errors.Count > 0)
            return PreflightCheckResult.Fail(
                $"{errors.Count} validation error(s): {string.Join(" | ", errors)}",
                "Fix the listed agentsmith.yml entries — the server refuses to start while these persist.");

        var emptySecrets = config.Secrets
            .Where(s => string.IsNullOrEmpty(s.Value))
            .Select(s => s.Key)
            .ToList();
        if (emptySecrets.Count > 0)
            return PreflightCheckResult.Fail(
                $"secret(s) resolved to empty: {string.Join(", ", emptySecrets)}",
                "Export the environment variable each secrets.<name> entry references — an unresolved "
                + "${VAR} placeholder silently becomes an empty string and surfaces later as a bad credential.");

        return PreflightCheckResult.Pass(
            $"config OK: {config.Agents.Count} agent(s), {config.Trackers.Count} tracker(s), "
            + $"{config.Repos.Count} repo(s), {config.Projects.Count} project(s), "
            + $"{config.Secrets.Count} secret(s) resolved");
    }
}
