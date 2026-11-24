namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-6811c: concludes a merge whose conflicts a resolver worked on. The commit is made here, never by the
/// resolver, so a model's claim alone never ends a merge. 2026-11-24-9dc0a: the resolver answers what the server's check
/// (<see cref="BaseMergeWholeness"/>) still finds open, which also staged what was resolved; only a whole one is committed.
/// </summary>
public sealed class BaseMergeResolutionCommit(BaseRefreshGit git)
{
    public async Task<BaseMergeResolution> CommitAsync(AgentSmith.Contracts.Sandbox.ISandbox sandbox, IReadOnlyList<string> rest,
        Func<IReadOnlyList<string>, CancellationToken, Task<BaseMergeResolution>> resolveRest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolveRest);
        var resolution = await resolveRest(rest, ct);
        if (!resolution.IsWhole) return resolution;
        var commit = await git.RunAsync(sandbox, ["commit", "--no-edit", "--no-verify"], ct);
        return commit.ExitCode == 0 ? resolution
            : resolution with { Open = rest, Reason = $"git could not commit the resolved merge: {commit.ErrorMessage}" };
    }
}
