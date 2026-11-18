using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Deployment;

/// <summary>
/// 2026-11-18-3863f: reads one deploy record's host — the pipeline run through its runner, or the pull request's state
/// and then its CI on the head that read saw — bounded by the project's merge.ci_excerpt_chars and
/// merge.ci_failed_checks_read, and settles it by <see cref="DeployFollowVerdict"/>. It only reads hosts. A record whose
/// project or binding no longer resolves to the repository it went to is settled with that reason; a host that fails a
/// read leaves the record as it was, read again when next due.
/// </summary>
public sealed class DeployFollow(ISourceProviderFactory sources, IPipelineRunnerFactory runners, ILogger<DeployFollow> logger)
{
    public async Task FollowAsync(DeployRecord record, ResolvedProject? project, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        var (repo, gone) = Repo(record, project);
        if (repo is null) { Settle(record, now, gone); return; }
        var bounds = new CiReadRequest(null, project!.Merge.CiExcerptChars, project.Merge.CiFailedChecksRead);
        try
        {
            await (record.PipelineRunId is { } run ? PipelineAsync(record, repo, run, bounds, ct) : PullRequestAsync(record, repo, bounds, ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Deploy {RunId} could not be read at {Url}; read again when due", record.RunId, record.Url);
        }
        record.CheckedAtTicks = now.UtcTicks;
        var (settled, note) = DeployFollowVerdict.Of(record, project.Merge, now);
        if (settled) Settle(record, now, note);
    }

    private async Task PipelineAsync(DeployRecord record, RepoConnection repo, string runId, CiReadRequest bounds, CancellationToken ct)
    {
        var runner = runners.Create(repo) ?? throw new InvalidOperationException($"`{repo.Name}`'s host runs no pipeline");
        var state = await runner.ReadAsync(runId, bounds, ct);
        (record.CiState, record.CiFailuresJson) = (state.State, DeployRecordRepository.Failures(state.Failures));
    }

    private async Task PullRequestAsync(DeployRecord record, RepoConnection repo, CiReadRequest bounds, CancellationToken ct)
    {
        var provider = sources.Create(repo);
        if (provider is not IPrReviewStateReader states) throw new InvalidOperationException($"`{repo.Name}`'s host reads no pull request");
        var state = await states.ReadAsync(record.PullRequestUrl!, ct);
        record.PullRequestState = state.State;
        if (provider is not IPullRequestCiReader reader) { record.CiState = CiStates.None; return; }
        var ci = await reader.ReadCiAsync(record.PullRequestUrl!, bounds with { HeadSha = state.HeadSha }, ct);
        (record.CiState, record.CiFailuresJson) = (ci.State, DeployRecordRepository.Failures(ci.Failures));
    }

    private static (RepoConnection? Repo, string? Gone) Repo(DeployRecord record, ResolvedProject? project)
    {
        if (project is null) return (null, $"project '{record.Project}' is no longer configured, so the deploy is no longer followed");
        var (target, error) = DeploymentTarget.Of(project, new DeployRequest(record.Context, record.ContextRepo, record.Environment, null));
        var (repo, repoError) = target is null ? (null, error) : target.RepoOf(project, target.Environment);
        return repo is not null && string.Equals(repo.Name, record.Repo, StringComparison.OrdinalIgnoreCase)
            ? (repo, null)
            : (null, $"the binding no longer resolves to `{record.Repo}` ({repoError ?? $"it names `{repo?.Name}`"}), so the deploy is no longer followed");
    }

    private static void Settle(DeployRecord record, DateTimeOffset now, string? note) =>
        (record.SettledAtTicks, record.Note) = (now.UtcTicks, note);
}
