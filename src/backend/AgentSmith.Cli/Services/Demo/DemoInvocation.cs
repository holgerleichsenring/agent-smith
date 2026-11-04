namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: the demo verb's parsed inputs. AgentName defaults to the first entry
/// in the config's agents: catalog; WorkspaceDir defaults to a fresh temp dir.
/// </summary>
internal sealed record DemoInvocation(
    string ConfigPath,
    string? AgentName = null,
    string? WorkspaceDir = null)
{
    /// <summary>2026-11-02-2b26a: decided before the container is built, so a generated config can sit beside it.</summary>
    public static string DefaultWorkspaceDir() => Path.Combine(
        Path.GetTempPath(), $"agentsmith-demo-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
}
