using AgentSmith.Contracts.Services;

namespace AgentSmith.Cli;

internal static class ConfigDiscovery
{
    private static readonly string[] SearchPaths =
    [
        Path.Combine(Directory.GetCurrentDirectory(), ProjectMetaPaths.ConfigYaml),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "agentsmith.yml"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ProjectMetaPaths.ConfigYaml),
    ];

    public static string Resolve()
    {
        foreach (var path in SearchPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Fall back to legacy default (works when running from repo root)
        return "config/agentsmith.yml";
    }
}
