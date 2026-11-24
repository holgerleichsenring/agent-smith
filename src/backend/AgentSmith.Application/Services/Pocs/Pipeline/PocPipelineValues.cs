using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>2026-11-23-933dd: what a target's build pipeline is rendered from — its folders, the Trivy image, where the
/// image is pushed (&lt;login_server&gt;/&lt;repository&gt;, the poc and the sha appended by the push job) and the build
/// block's names and job timeouts.</summary>
public sealed record PocPipelineValues(
    string Root, string TemplatePath, string TrivyImage, string RegistryHost, string ImageRepository, PocBuildConfig Build)
{
    public static PocPipelineValues Of(PocTargetConfig target, PocScanConfig scan)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(scan);
        var registry = target.Registry ?? new PocRegistryConfig();
        return new PocPipelineValues(target.Root, target.TemplatePath, scan.TrivyImage, registry.LoginServer,
            $"{registry.LoginServer}/{registry.Repository}", target.Build);
    }
}
