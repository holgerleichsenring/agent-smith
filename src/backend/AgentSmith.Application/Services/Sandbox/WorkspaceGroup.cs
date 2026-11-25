using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the contexts of one repository that build with one toolchain image — one sandbox key of the run,
/// served by that image's container of the workspace. <see cref="Spec"/> carries the group's merged resources.
/// </summary>
public sealed record WorkspaceGroup(
    RepoConnection Repo,
    IReadOnlyList<RemoteContextDiscovery> Discoveries,
    SandboxSpec Spec,
    int RepoGroupCount)
{
    public string Image => Spec.ToolchainImage;

    /// <summary>The group's identity in the run — one repository, one image.</summary>
    public string Identity => $"{Repo.Name}\n{Image}";
}
