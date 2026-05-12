namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Optional per-project sandbox overrides. When unset, the sandbox toolchain
/// image is auto-detected (p0135): host-side project-map.json cache from a
/// prior AnalyzeProjectHandler run is consulted first, then the remote
/// .agentsmith/context.yaml via ISourceProvider.TryReadFileAsync, then the
/// in-memory ProjectMap, then a generic toolchain image.
/// </summary>
public sealed class SandboxConfig
{
    /// <summary>
    /// Override-only. When set, wins over every auto-detection layer. Use this
    /// for operators with strict base-image policies (e.g. internal mirror,
    /// pinned digests for supply-chain reasons) or for projects whose
    /// language doesn't fit the convention table (rare). For typical .NET /
    /// Node / Python / Go / Rust projects, leave unset and let auto-detection
    /// pick the matching public image.
    /// </summary>
    public string? ToolchainImage { get; set; }
}
