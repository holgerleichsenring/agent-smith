using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7f: undoes a multi-repository spec write that failed in one member, so the series
/// lands in all its repositories or in none. Only the members PUSHED in this write are reverted — a
/// revert commit of our own, pushed plainly: a person's commit in between refuses it and is never
/// overwritten. Every member's tree is then put back at its pre-write HEAD, so nothing the run
/// persists later pushes the half. A member whose push created the branch keeps the branch with the
/// spec and its revert, and reads as nothing at the series path.
/// </summary>
public sealed class SeriesWriteRollback(
    SandboxGitOperations gitOps, SeriesMemberCommit member, ILogger<SeriesWriteRollback> logger)
{
    public async Task<SpecSetWriteResult> UndoAsync(
        IReadOnlyList<(RepoConnection Repo, ISandbox Sandbox)> targets, SpecSet set, string branch,
        SeriesWriteProgress progress, string failure, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var reverts = new Dictionary<string, string>(StringComparer.Ordinal);
        var refused = new List<string>();
        foreach (var (repo, sandbox) in targets.Where(t => progress.Pushed.GetValueOrDefault(t.Repo.Name)))
        {
            try
            {
                await gitOps.StageRevertAsync(sandbox, progress.Shas[repo.Name], ct);
                await gitOps.CommitAndPushStagedAsync(sandbox, branch, RevertMessage(set, repo.Name, failure), repo, ct, plain: true);
                reverts[repo.Name] = await gitOps.GetHeadCommitAsync(sandbox, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The revert of series {Base} in {Repo} was refused", set.Series, repo.Name);
                refused.Add($"{repo.Name} ({FirstLine(ex)})");
            }
        }
        foreach (var (repo, sandbox) in targets)
            await ResetAsync(sandbox, repo, progress.Before.GetValueOrDefault(repo.Name), set, ct);
        return SpecSetWriteResult.RolledBack(Reason(failure, reverts, refused), progress.Pushed, reverts);
    }

    private async Task ResetAsync(ISandbox sandbox, RepoConnection repo, string? before, SpecSet set, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(before)) return;
        try
        {
            await gitOps.ResetToAsync(sandbox, before, member.PathsOf(set.OfRepository(repo.Name)), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resetting {Repo} to {Sha} after a failed spec write failed", repo.Name, before);
        }
    }

    private static string Reason(string failure, IReadOnlyDictionary<string, string> reverts, IReadOnlyList<string> refused) =>
        $"the spec write failed in {failure}"
        + (reverts.Count > 0 ? $"; reverted in {string.Join(", ", reverts.Keys)}" : string.Empty)
        + (refused.Count > 0 ? $"; the revert was refused in {string.Join(", ", refused)}, which keep the new revision" : string.Empty);

    private static string RevertMessage(SpecSet set, string repo, string failure) =>
        $"spec: revert {set.Series} ({set.Key}) revision {set.Current.Number} in {repo} — the write failed in {failure}";

    private static string FirstLine(Exception ex) => ex.Message.Split('\n', 2)[0].Trim();
}
