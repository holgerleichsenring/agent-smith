using AgentSmith.Application.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: one build, idempotent in this order — (1) the tag already in the registry: taken, no pipeline;
/// (2) a stored run is read first: pending or green it is followed, red with a blocking report the revision is blocked,
/// red without one its id is cleared; (3) otherwise a run is started and its id stored AT ONCE, before the first read,
/// so a restarted run follows the same pipeline. A start the host does not name fails. A run started here that ends red
/// without a blocking report fails with its failures; its id stays, and the next retry starts afresh by (2).
/// </summary>
public sealed class PocBuildRun(
    PocBuildRegistry registry, IPipelineRunnerFactory runners, PocBuildStart start, PocBuildFollow follow,
    PocBuildOutcome outcome, Contracts.Pocs.IPocBuildStore store)
{
    public async Task<CommandResult> RunAsync(PocBuildJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (await registry.PushedAsync(job, ct) is { } pushed) return await outcome.TakenAsync(job, pushed, ct);
        if (runners.Create(job.Repo) is not { } runner)
            return CommandResult.Fail($"The source repo '{job.Target.SourceRepo}' of target '{job.Target.Name}' is a {job.Repo.Type} "
                + "repository, whose host runs no pipeline; PoC images are built by a GitHub, Azure DevOps or GitLab pipeline.");
        var bounds = new CiReadRequest(null, job.Context.Project.Merge.CiExcerptChars, job.Context.Project.Merge.CiFailedChecksRead);
        if (job.Subject.PipelineRunId is { } stored && await StoredAsync(runner, stored, job, bounds, ct) is { } settled) return settled;
        var (runId, refused) = await start.StartAsync(runner, job, ct);
        if (runId is null) return CommandResult.Fail(refused!);
        return await FollowAsync(runner, runId, job, bounds, ct);
    }

    // A stored run that is pending or green is followed; red with a blocking report blocks; red otherwise is cleared.
    private async Task<CommandResult?> StoredAsync(IPipelineRunner runner, string runId, PocBuildJob job, CiReadRequest bounds, CancellationToken ct)
    {
        var state = await runner.ReadAsync(runId, bounds, ct);
        if (state.State != CiStates.Red) return await FollowAsync(runner, runId, job, bounds, ct);
        if (await outcome.BlockedAsync(runner, runId, job, ct) is { } blocked) return blocked;
        await store.RecordPipelineRunAsync(job.Subject.PocId, job.Subject.Revision, null, null, ct);
        return null;
    }

    private async Task<CommandResult> FollowAsync(IPipelineRunner runner, string runId, PocBuildJob job, CiReadRequest bounds, CancellationToken ct)
    {
        var (state, late) = await follow.FollowAsync(runner, runId, job, bounds, ct);
        if (state is null) return CommandResult.Fail($"Revision {job.Subject.Revision} of '{job.Subject.PocName}': {late}.");
        return state.State == CiStates.Green
            ? await outcome.GreenAsync(runner, runId, job, ct)
            : await outcome.BlockedAsync(runner, runId, job, ct) ?? outcome.Failed(job, state);
    }
}
