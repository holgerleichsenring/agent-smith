using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 api-security-scan boundary swap: replaces the docker-spawning
/// scanners (Nuclei / Spectral / ZAP) with empty-findings stubs. The
/// real scanners' docker containers are an external dependency outside
/// this harness's scope; what we exercise is the post-scanner handler
/// chain (AgenticMaster + DeliverFindings) over the shape the scanners
/// publish into PipelineContext.
/// </summary>
internal static class ApiScannerStubs
{
    public static void Register(IServiceCollection services)
    {
        services.RemoveAll<INucleiScanner>();
        services.AddSingleton<INucleiScanner, EmptyNucleiScanner>();
        services.RemoveAll<ISpectralScanner>();
        services.AddSingleton<ISpectralScanner, EmptySpectralScanner>();
        services.RemoveAll<IZapScanner>();
        services.AddSingleton<IZapScanner, EmptyZapScanner>();
    }

    private sealed class EmptyNucleiScanner : INucleiScanner
    {
        public Task<NucleiResult> ScanAsync(string targetUrl, string swaggerPath, CancellationToken ct) =>
            Task.FromResult(new NucleiResult([], 0, string.Empty));
    }

    private sealed class EmptySpectralScanner : ISpectralScanner
    {
        public Task<SpectralResult> LintAsync(string swaggerPath, CancellationToken ct) =>
            Task.FromResult(new SpectralResult([], 0, 0, 0));
    }

    private sealed class EmptyZapScanner : IZapScanner
    {
        public Task<ZapResult> ScanAsync(ZapScanRequest request, CancellationToken ct) =>
            Task.FromResult(new ZapResult([], 0, request.ScanType));
    }
}
