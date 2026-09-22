using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: in a multi-repo run the spec set lives in exactly ONE repo — the first
/// of the resolved scope — and the pointer records which. On a later run whose
/// scope changed, the recorded repo wins as long as it is still in scope, so the
/// pointer cannot end up aiming at a repo that was never checked out.
/// <para>
/// 2026-09-22-b6ad: the APPROVAL may already name one, because filing wrote the branch before any
/// run existed. It ranks BELOW the pointer, which is the last repository this system actually
/// committed into, and ABOVE the first-scoped fallback, which is a guess: a glob-configured
/// repository list is expanded in the discovery snapshot's order, so "the first one" can move
/// between the approval and the first run, and the branch would then be looked for in a
/// repository that never carried it.
/// </para>
/// </summary>
public static class SpecCarryingRepoResolver
{
    /// <param name="approvedCarrier">The repository the approval chose, or null/empty when none did.</param>
    public static RepoConnection? Resolve(
        IReadOnlyList<RepoConnection> scopedRepos, SpecSetPointer? pointer,
        string? approvedCarrier = null)
    {
        if (scopedRepos is null || scopedRepos.Count == 0) return null;
        var named = pointer is null || string.IsNullOrWhiteSpace(pointer.CarryingRepo)
            ? approvedCarrier : pointer.CarryingRepo;
        if (string.IsNullOrWhiteSpace(named)) return scopedRepos[0];
        return scopedRepos.FirstOrDefault(
            r => string.Equals(r.Name, named, StringComparison.OrdinalIgnoreCase))
            ?? scopedRepos[0];
    }

    /// <summary>
    /// 2026-09-22-b6ad: the repository FILING picks, spelled so it agrees with
    /// <see cref="Resolve"/> by construction rather than by coincidence — the first of the
    /// project's configured repositories that the approval named, which is exactly the first
    /// element of the scoped list a run of that approval resolves.
    /// </summary>
    public static string ChooseCarrier(
        IReadOnlyList<RepoConnection> projectRepos, IReadOnlyList<string> approvedRepos)
    {
        if (projectRepos is null || approvedRepos is null) return string.Empty;
        return projectRepos.FirstOrDefault(
            r => approvedRepos.Any(n => string.Equals(n, r.Name, StringComparison.OrdinalIgnoreCase)))
            ?.Name ?? string.Empty;
    }
}
