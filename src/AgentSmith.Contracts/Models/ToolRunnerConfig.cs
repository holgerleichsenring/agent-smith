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
    public Dictionary<string, string> Images { get; set; } = new()
    {
        ["nuclei"] = "projectdiscovery/nuclei:latest",
        ["spectral"] = "stoplight/spectral:6",
    };
}
