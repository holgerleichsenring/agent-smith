using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-27-6625: a revision that moves its spec to a repository the run's workspace lacks brings that repository in
/// through ensure_repo_sandbox — the one way a repository joins a run — before the spec is placed there. Whether it
/// joined is read from the run (its repositories and their sandboxes), never from the answer's wording.
/// </summary>
public sealed class SpecRepoJoin(SandboxTargets targets)
{
    /// <param name="ensure">ensure_repo_sandbox of the master revising; null where the master has none.</param>
    /// <returns>Null once every repository the revision names has a sandbox; else why the revision cannot land.</returns>
    public async Task<string?> RefusalAsync(
        PipelineContext pipeline, SpecDraft current, SpecDraft revised,
        Func<string, CancellationToken, Task<string>>? ensure, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(revised);
        foreach (var name in revised.Repos.Where(r => !current.Repos.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            if (Joined(pipeline, name)) continue;
            var answer = ensure is null ? "this master cannot bring a repository into the workspace" : await ensure(name, ct);
            if (!Joined(pipeline, name))
                return $"repository '{name}' could not join the run's workspace ({answer})";
        }
        return null;
    }

    private bool Joined(PipelineContext pipeline, string name) =>
        pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null
        && repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) is { } repo
        && targets.SandboxesForRepo(pipeline, repo).Count > 0;
}
