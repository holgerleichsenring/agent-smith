namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-08-31-7097: optional marker — this sandbox runs a toolchain IMAGE the backend
/// pulled, and names it. The container backends implement it; the in-process backend
/// executes on the host and does not, so anything reporting about a sandbox's tools
/// names an image only when one was actually pulled.
/// </summary>
public interface ISandboxToolchainImage
{
    /// <summary>The image reference the backend started this sandbox from.</summary>
    string ToolchainImage { get; }
}
