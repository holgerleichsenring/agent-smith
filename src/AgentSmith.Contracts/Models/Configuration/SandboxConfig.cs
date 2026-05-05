namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Optional per-project sandbox overrides. When unset, SandboxSpecBuilder falls
/// back to convention-based image resolution from ProjectMap.PrimaryLanguage.
/// </summary>
public sealed class SandboxConfig
{
    public string? ToolchainImage { get; set; }
}
