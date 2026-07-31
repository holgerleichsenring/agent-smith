using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: in a multi-repo run the spec lives in exactly ONE repo — the first of
/// the resolved scope — and the pointer records which. On a later run whose scope
/// changed, the recorded repo wins as long as it is still in scope, so the
/// pointer cannot end up aiming at a repo that was never checked out.
/// </summary>
public static class WorkSpecCarryingRepoResolver
{
    public static RepoConnection? Resolve(
        IReadOnlyList<RepoConnection> scopedRepos, WorkSpecPointer? pointer)
    {
        if (scopedRepos is null || scopedRepos.Count == 0) return null;
        if (pointer is null || string.IsNullOrWhiteSpace(pointer.CarryingRepo))
            return scopedRepos[0];
        return scopedRepos.FirstOrDefault(
            r => string.Equals(r.Name, pointer.CarryingRepo, StringComparison.OrdinalIgnoreCase))
            ?? scopedRepos[0];
    }
}
