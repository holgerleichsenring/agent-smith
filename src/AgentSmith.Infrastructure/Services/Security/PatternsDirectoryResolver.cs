namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Resolves the patterns directory from standard config locations and the
/// AGENTSMITH_CONFIG_DIR override.
/// </summary>
public sealed class PatternsDirectoryResolver
{
    public string Resolve()
    {
        var candidates = new[]
        {
            Path.Combine("config", "patterns"),
            Path.Combine(AppContext.BaseDirectory, "config", "patterns"),
        };

        var configDir = Environment.GetEnvironmentVariable("AGENTSMITH_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            return Directory.Exists(Path.Combine(configDir, "patterns"))
                ? Path.Combine(configDir, "patterns")
                : Directory.Exists(Path.Combine(configDir, "config", "patterns"))
                    ? Path.Combine(configDir, "config", "patterns")
                    : candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }
}
