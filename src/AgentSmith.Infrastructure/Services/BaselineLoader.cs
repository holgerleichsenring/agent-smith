using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Resolution order, mirroring PatternsDirectoryResolver:
///   1. AGENTSMITH_CONFIG_DIR/baselines/{name}.yaml
///   2. {skill-catalog-root}/baselines/{name}.yaml
///   3. baselines/{name}.yaml relative to base directory or cwd (dev fallback)
/// </summary>
public sealed class BaselineLoader(
    ISkillsCatalogPath catalogPath,
    ILogger<BaselineLoader> logger) : IBaselineLoader
{
    public string? Load(string baselineName)
    {
        foreach (var path in EnumerateCandidates(baselineName))
        {
            if (!File.Exists(path)) continue;
            try
            {
                var content = File.ReadAllText(path);
                logger.LogInformation("Loaded baseline {Name} from {Path}", baselineName, path);
                return content;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read baseline {Path}", path);
            }
        }
        logger.LogInformation("Baseline {Name} not found in any known location", baselineName);
        return null;
    }

    private IEnumerable<string> EnumerateCandidates(string baselineName)
    {
        var fileName = baselineName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            ? baselineName
            : $"{baselineName}.yaml";

        var configDir = Environment.GetEnvironmentVariable("AGENTSMITH_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            yield return Path.Combine(configDir, "baselines", fileName);
            yield return Path.Combine(configDir, "config", "baselines", fileName);
        }

        string? catalogRoot = null;
        try { catalogRoot = catalogPath.Root; }
        catch (InvalidOperationException) { /* catalog not bootstrapped yet */ }
        if (catalogRoot is not null)
            yield return Path.Combine(catalogRoot, "baselines", fileName);

        yield return Path.Combine("baselines", fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "baselines", fileName);
    }
}
