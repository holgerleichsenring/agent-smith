using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// 2026-10-12-6bfab: the sandbox image boundary in force, on the wire — the registries in the
/// canonical form they are matched in, whether they are the built-in default, plus the built-in
/// default list itself so the dashboard never hard-codes it. 2026-11-16-f3fbd: the library is an
/// entry of that list, so no switch travels beside it.
/// </summary>
public sealed record ImageTrustResponse(
    IReadOnlyList<string> Registries,
    bool RegistriesAreDefault,
    IReadOnlyList<string> DefaultRegistries,
    string Description)
{
    /// <summary>Projects one boundary onto the wire.</summary>
    public static ImageTrustResponse Map(RegistryTrustBoundary boundary) => new(
        boundary.Registries,
        boundary.RegistriesAreDefault,
        RegistryTrustBoundary.DefaultRegistries,
        boundary.Description);
}
