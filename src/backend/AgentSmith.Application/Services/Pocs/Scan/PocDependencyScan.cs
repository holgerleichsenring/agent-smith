using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the dependency scan of a PoC — where the sandbox starts. A one-shot sandbox of the pinned
/// pocs.scan.osv_image (no project secrets, no shared package cache) is given the lockfiles from memory and runs the
/// scanner; nothing of the upload is installed or executed. No lockfile, or exit 128 (no packages found), is a note
/// 'dependencies not checked'; exit 0 or 1 with a parseable report is a result; anything else fails the run naming it.
/// It fails closed: an unpinned image or a backend that runs no image is a failure, never a clean pass.
/// </summary>
public sealed class PocDependencyScan(
    ISandboxFactory factory, SandboxSpecBuilder specs, IRunContextAccessor runContext, ReferenceSetMaterialiser materialiser,
    PocScannedLockfiles lockfiles, PocOsvRun osv, PocOsvReportReader reports, OsvReportParser parser, PocFindingIds ids,
    SandboxContainerRuntime? runtime = null)
{
    private const int NoPackagesFound = 128; // not a limit: osv-scanner's exit code for "no packages found"

    public async Task<PocDependencyResult> ScanAsync(
        ResolvedProject project, IReadOnlyList<ReferenceSetFile> files, PocScanConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var (scanned, unpinned) = lockfiles.Select(files);
        IReadOnlyList<PocScanFinding> notes = unpinned is null ? [] : [unpinned];
        if (scanned.Count == 0) return new PocDependencyResult([], [.. notes, NotChecked("the upload holds no lockfile with a pinned dependency")]);
        if (!config.IsPinned())
            return PocDependencyResult.Failed($"{PocScanConfig.OsvImageKey} is '{config.OsvImage}'; the dependency scan runs only an image pinned by digest");
        if (runtime is { SpawnsContainers: false })
            return PocDependencyResult.Failed("the dependency scan needs a sandbox backend that runs images: Kubernetes or Docker");
        await using var sandbox = await factory.CreateAsync(Spec(project, config), ct);
        await materialiser.WriteUnderAsync(sandbox, scanned, PocOsvRun.LockfileRoot, ct);
        var result = await osv.RunAsync(sandbox, [.. scanned.Select(f => f.Path)], config, ct);
        return await ResultAsync(sandbox, result, notes, config, ct);
    }

    private async Task<PocDependencyResult> ResultAsync(
        ISandbox sandbox, AgentSmith.Sandbox.Wire.StepResult result, IReadOnlyList<PocScanFinding> notes, PocScanConfig config,
        CancellationToken ct)
    {
        if (result.TimedOut)
            return PocDependencyResult.Failed($"the dependency scan took longer than {PocScanConfig.TimeoutSecondsKey} ({config.TimeoutSeconds} s)");
        if (result.ExitCode == NoPackagesFound) return new PocDependencyResult([], [.. notes, NotChecked("the scanner found no packages in the lockfiles")]);
        if (result.ExitCode is not (0 or 1))
            return PocDependencyResult.Failed($"osv-scanner exited {result.ExitCode}: {(result.ErrorMessage ?? result.OutputContent ?? string.Empty).Trim()} "
                + $"(its vulnerability database comes from {PocOsvRun.DatabaseHost} through the sandbox's egress)");
        var found = await reports.ReadAsync(sandbox, ct) is { } json ? parser.Parse(json, $"{Repository.SandboxWorkPath}/{PocOsvRun.LockfileRoot}/") : null;
        return found is null ? PocDependencyResult.Failed($"osv-scanner's report ({PocOsvRun.Report}) could not be read or parsed") : new PocDependencyResult(found, notes);
    }

    private SandboxSpec Spec(ResolvedProject project, PocScanConfig config) =>
        specs.Build(project, language: null, pipelineName: null) with
        {
            ToolchainImage = config.OsvImage, Secrets = ResolvedSandboxSecrets.Empty, NoPackageCaches = true,
            RunId = runContext.CurrentRunId, StepTimeoutSeconds = config.TimeoutSeconds,
        };

    private PocScanFinding NotChecked(string why) =>
        new(ids.Note("dependencies-not-checked|" + why), PocScanFindingKinds.Note, "dependencies-not-checked", string.Empty, null,
            "Dependencies not checked", $"No dependency was checked for known vulnerabilities: {why}.", null, null, []);
}
