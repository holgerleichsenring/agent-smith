using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0356: one run_command per sandbox at master start reports what the
/// toolchain image actually provides; the distilled line enters the master
/// context as a capability statement ("This sandbox has: ..."). Probe failures
/// simply omit the section — never a fabricated inventory.
/// <para>
/// 2026-08-31-7097: the same sweep also carries the binaries this repository's DECLARED
/// verify stages name, so a tool the image lacks is reported before the stage dies on
/// it. It runs at master start, which is after EnsurePrerequisites — probing before the
/// step that installs the tools would accuse a good image.
/// </para>
/// </summary>
public sealed class SandboxToolchainProbe(
    ContextVerifyStagesResolver declaredStages,
    ToolchainFindingReporter findings,
    ILogger<SandboxToolchainProbe> logger) : ISandboxToolchainProbe
{
    private const int ProbeTimeoutSeconds = 45;

    public async Task<string?> ProbeAsync(
        PipelineContext pipeline,
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? keyToRepo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);
        var lines = new List<(string Name, string Capability)>();
        var reported = new List<string>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var name = keyToRepo is not null && keyToRepo.TryGetValue(key, out var repo)
                && !string.IsNullOrEmpty(repo) ? repo : key;
            if (lines.Any(l => l.Name == name)) continue;
            var derivation = DeclaredStageBinaries.Derive(declaredStages.For(pipeline, key));
            var stdout = await SweepAsync(sandbox, derivation, cancellationToken);
            reported.AddRange(findings.Report(name, ImageOf(pipeline, key), derivation, stdout));
            if (ToolchainCapabilityLine.Distill(stdout) is { } capability)
                lines.Add((name, capability));
        }
        return ToolchainSection.Render(lines, reported);
    }

    private async Task<string?> SweepAsync(
        ISandbox sandbox, DeclaredStageDerivation derivation, CancellationToken ct)
    {
        try
        {
            var command = ToolchainProbeCommand.For(derivation.Binaries);
            var output = await new SandboxStepRunner(sandbox).RunAsync(command, ProbeTimeoutSeconds, ct);
            return ToolchainCapabilityLine.ExtractStdout(output);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Toolchain probe failed — omitting the sandbox-toolchain section");
            return null;
        }
    }

    // The image the backend really pulled. An absent entry is the in-process backend,
    // which runs on the host: reporting an image nothing pulled would be a false report.
    private static string? ImageOf(PipelineContext pipeline, string key) =>
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxImages, out var images)
        && images is not null && images.TryGetValue(key, out var image)
            ? image
            : null;
}
