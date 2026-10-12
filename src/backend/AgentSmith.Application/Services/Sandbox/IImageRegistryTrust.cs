using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-12-6bfab: the sandbox image boundary in force right now — read at the moment
/// it is asked, so a saved change applies to the next decision without a restart.
/// </summary>
public interface IImageRegistryTrust
{
    /// <summary>The boundary one decision is made against; read once per decision.</summary>
    RegistryTrustBoundary InForce();
}
