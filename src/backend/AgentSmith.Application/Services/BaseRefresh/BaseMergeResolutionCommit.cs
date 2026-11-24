namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-6811c: concludes a merge whose conflicts a resolver worked on — the paths it was given staged with the ones it
/// wrote, and the merge committed only when nothing is left unmerged and no touched path carries a conflict marker. The
/// commit is made here, never by the resolver, so a model's claim alone never ends a merge.
/// </summary>
public sealed class BaseMergeResolutionCommit(BaseRefreshGit git)
{
    public async Task<bool> CommitAsync(AgentSmith.Contracts.Sandbox.ISandbox sandbox, IReadOnlyList<string> rest,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<string>?>> resolveRest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolveRest);
        if (await resolveRest(rest, ct) is not { } wrote) return false;
        IReadOnlyList<string> touched = [.. rest.Concat(wrote).Distinct(StringComparer.Ordinal)];
        if (!await git.StageAsync(sandbox, touched, ct)) return false;
        var unmerged = await git.RunAsync(sandbox, ["diff", "--name-only", "--diff-filter=U"], ct);
        if (unmerged.ExitCode != 0 || !string.IsNullOrWhiteSpace(unmerged.OutputContent)) return false;
        if (await git.HasMarkersAsync(sandbox, touched, ct)) return false;
        return (await git.RunAsync(sandbox, ["commit", "--no-edit", "--no-verify"], ct)).ExitCode == 0;
    }
}
