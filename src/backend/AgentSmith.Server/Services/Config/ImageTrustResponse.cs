using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-10-12-6bfab: the sandbox image boundary in force, on the wire — the registries in the
/// canonical form they are matched in, whether they are the built-in default, the library
/// switch and whether the operator set it or it follows the list, plus the built-in default
/// list itself so the dashboard never hard-codes it.
/// </summary>
public sealed record ImageTrustResponse(
    IReadOnlyList<string> Registries,
    bool RegistriesAreDefault,
    bool DockerHubLibrary,
    bool DockerHubLibraryIsExplicit,
    IReadOnlyList<string> DefaultRegistries,
    string Description)
{
    /// <summary>Projects one boundary onto the wire.</summary>
    public static ImageTrustResponse Map(RegistryTrustBoundary boundary) => new(
        boundary.Registries,
        boundary.RegistriesAreDefault,
        boundary.DockerHubLibrary,
        boundary.DockerHubLibraryIsExplicit,
        RegistryTrustBoundary.DefaultRegistries,
        boundary.Description);
}
