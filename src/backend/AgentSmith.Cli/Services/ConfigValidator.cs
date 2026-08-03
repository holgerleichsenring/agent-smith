using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0391b: answers "would the server accept this configuration?" without starting one.
///
/// It runs the SERVER's rules — the shared raw-to-typed pipeline behind
/// <see cref="IConfigurationLoader"/>, which records into <see cref="IStartupFindings"/>,
/// followed by <see cref="AgentSmithConfigValidator"/>, which is what the server's
/// ConfigurationProbe asks. There is deliberately no CLI-side rule: a second validator
/// would be a second source of truth about what a valid configuration is, and the two
/// would drift the first time one of them was extended.
/// </summary>
public sealed class ConfigValidator(
    IConfigurationLoader loader,
    IStartupFindings findings,
    AgentSmithConfigValidator validator)
{
    public IReadOnlyList<StartupFinding> Validate(string configPath)
    {
        try
        {
            var config = loader.LoadConfig(configPath);
            foreach (var finding in validator.Findings(config)) findings.Record(finding);
        }
        catch (ConfigurationException ex)
        {
            // The file loader refuses a configuration it cannot materialize (p0391b: the
            // one-shot path's exit code is its report). The findings behind that refusal are
            // already recorded; only an unparseable FILE has none, so that one is added here.
            if (!findings.All.Any(f => f.IsBlocking)) findings.Record(Unparseable(configPath, ex));
        }
        return findings.All;
    }

    private static StartupFinding Unparseable(string configPath, ConfigurationException ex) => new(
        StartupSubsystems.ConfigFile,
        StartupFindingSeverity.Blocking,
        $"'{configPath}' could not be read as a configuration: {ex.Message}",
        Field: "CONFIG_PATH");
}
