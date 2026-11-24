using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: what a build ended as. Red with the gate failed AND a blocking finding: the revision is blocked at
/// the image stage (a PoC that never reached ready with it). Red otherwise: the step fails with the run's failures.
/// Green: the report's findings are kept as notes, the pushed digest is read and must be what the registry resolves the
/// tag to; the revision is built and the digest published for the GitOps step.
/// </summary>
public sealed class PocBuildOutcome(PocBuildReport reports, PocBuildRegistry registry, IPocBuildStore store, PocBuildOverrule overrule)
{
    public async Task<CommandResult> TakenAsync(PocBuildJob job, string digest, CancellationToken ct)
    {
        var notes = job.Subject.ImageVerdictJson ?? new PocScanVerdict([], []).ToJson();
        return await BuiltAsync(job, digest, notes, "is already in the registry; no pipeline was started", ct);
    }

    public async Task<CommandResult?> BlockedAsync(IPipelineRunner runner, string runId, PocBuildJob job, CancellationToken ct)
    {
        var report = await reports.ReadAsync(runner, runId, job, ct);
        if (report.Problem is not null) return Fail(job, report.Problem);
        if (!report.Blocks) return null;
        await store.RecordImageBlockAsync(job.Subject.PocId, job.Subject.Revision, report.Verdict!.ToJson(), ct);
        return CommandResult.Fail($"Revision {job.Subject.Revision} of '{job.Subject.PocName}' is blocked by the image scan with "
            + $"{report.Verdict.Blocks.Count} finding(s); nothing was pushed.");
    }

    public async Task<CommandResult> GreenAsync(IPipelineRunner runner, string runId, PocBuildJob job, CancellationToken ct)
    {
        var report = await reports.ReadAsync(runner, runId, job, ct);
        if (report.Problem is not null) return Fail(job, report.Problem);
        var pushed = await runner.ReadArtifactTextAsync(runId, PocBuildArtifacts.Digest, PocBuildArtifacts.SmallFileBytes, ct);
        if (pushed?.Text?.Trim() is not { Length: > 0 } digest)
            return Fail(job, "the build ended green but left no digest (artifact poc-image, digest.txt)");
        if (await registry.VerifyAsync(job, digest, ct) is { } mismatch) return Fail(job, mismatch);
        var verdict = report.Verdict ?? new PocScanVerdict([], []);
        var notes = new PocScanVerdict([], [.. verdict.Notes, .. verdict.Blocks.Select(b => b with { Kind = PocScanFindingKinds.Note })]);
        await overrule.RecordAsync(job, ct);
        return await BuiltAsync(job, digest, notes.ToJson(), $"was built, scanned and pushed ({notes.Notes.Count} note(s))", ct);
    }

    public CommandResult Failed(PocBuildJob job, PipelineRunState state) =>
        Fail(job, $"the build ended red without a blocking scan report ({state.Url}): "
            + string.Join("; ", state.Failures.Select(f => $"{f.Name}{(f.Url is null ? string.Empty : $" ({f.Url})")}: {f.Excerpt}")));

    private async Task<CommandResult> BuiltAsync(PocBuildJob job, string digest, string verdictJson, string how, CancellationToken ct)
    {
        await store.RecordBuiltAsync(job.Subject.PocId, job.Subject.Revision, digest, verdictJson, ct);
        job.Context.Pipeline.Set(ContextKeys.PocImageDigest, digest);
        return CommandResult.Ok($"Revision {job.Subject.Revision} of '{job.Subject.PocName}' {how}: {digest}.");
    }

    private static CommandResult Fail(PocBuildJob job, string why) =>
        CommandResult.Fail($"Revision {job.Subject.Revision} of '{job.Subject.PocName}' was not built: {why}.");
}
