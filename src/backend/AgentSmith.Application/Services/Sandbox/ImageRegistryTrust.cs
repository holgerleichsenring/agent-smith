using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-25-014d: whether a toolchain image comes from a source the operator trusts. The
/// image string is model-authored (context.yaml stack.image), so this is the supply-chain
/// boundary the run is held to — and which registries sit inside it is the operator's to say,
/// through <see cref="SandboxGlobalConfig.AllowedRegistries"/>.
/// <para>
/// 2026-10-12-6bfab: the boundary is read at each decision, not once at composition — the
/// sandbox options are built once per process, so a change saved in the Studio never reached
/// a running server. A read that FAILS keeps the last boundary read; with none, the boundary
/// is closed. Neither can widen what the operator narrowed, and a closed boundary costs
/// nothing but the declared image: a refused stack.image falls through to the catalog.
/// </para>
/// </summary>
public sealed class ImageRegistryTrust(
    ISandboxSettingsReader? settings = null,
    ILogger<ImageRegistryTrust>? logger = null) : IImageRegistryTrust
{
    private RegistryTrustBoundary? _last;

    public RegistryTrustBoundary InForce()
    {
        try
        {
            return _last = RegistryTrustBoundary.From(settings?.Current());
        }
        catch (Exception ex)
        {
            var kept = _last ?? RegistryTrustBoundary.Closed;
            logger?.LogWarning(ex,
                "The sandbox image boundary could not be read — keeping {Boundary}", kept.Description);
            return kept;
        }
    }
}
