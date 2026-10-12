namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36e: decides the repository every spec a derivation or design dialog cuts
/// names — the field placement reads.
/// <para>
/// 2026-10-12-f5f7a: ONE SPEC, ONE REPOSITORY. In a scope of several repositories every spec
/// names exactly one in-scope repository: one naming several is asked to split, one naming
/// none is asked to state it — nothing is filled, because "the first repository" or "all of
/// them" is a guess. One repository in scope is filled and never re-asked: there is nothing
/// for the model to decide.
/// </para>
/// </summary>
public sealed class SpecReposRule
{
    /// <param name="stated">What each spec stated, in order; an empty entry stated nothing.</param>
    /// <param name="scope">The scope's repository names; empty when no scope is known.</param>
    public SpecReposResolution Resolve(IReadOnlyList<IReadOnlyList<string>> stated, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(stated);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Count == 0) return new SpecReposResolution(stated, stated.Any(s => s.Count > 0), null);
        if (scope.Count == 1) return new SpecReposResolution([.. stated.Select(_ => scope)], false, null);
        return Checked(stated, scope);
    }

    /// <summary>The first <c>repo/context</c> a spec declares for a repository its own
    /// <c>repos</c> does not name, as the reason to ask again; null when they agree.</summary>
    public string? ContextDisagreement(IReadOnlyList<string> repos, IReadOnlyList<string> contexts, int position)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(contexts);
        foreach (var context in contexts)
        {
            var slash = context.IndexOf('/');
            if (slash <= 0) continue;
            var repo = context[..slash];
            if (!repos.Any(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase)))
                return $"spec {position} declares context '{context}' but its repos does not name '{repo}' — "
                    + "a spec that changes a repository's context changes that repository, so name it in repos";
        }
        return null;
    }

    private static SpecReposResolution Checked(IReadOnlyList<IReadOnlyList<string>> stated, IReadOnlyList<string> scope)
    {
        var decided = new List<IReadOnlyList<string>>(stated.Count);
        for (var i = 0; i < stated.Count; i++)
        {
            var (named, refusal) = One(stated[i], i + 1, scope);
            if (refusal is not null) return refusal;
            decided.Add(named);
        }
        return new SpecReposResolution(decided, true, null);
    }

    // Exactly one in-scope repository, in the scope's spelling — or why the spec is asked again.
    private static (IReadOnlyList<string> Named, SpecReposResolution? Refusal) One(
        IReadOnlyList<string> stated, int position, IReadOnlyList<string> scope)
    {
        var allowed = string.Join(", ", scope);
        if (stated.FirstOrDefault(r => InScope(r, scope) is null) is { } outside)
            return ([], SpecReposResolution.Refused(
                $"spec {position} names repository '{outside}' in repos, which is not in scope — name only: {allowed}"));
        IReadOnlyList<string> named = [.. stated.Select(r => InScope(r, scope)!).Distinct()];
        return named.Count switch
        {
            0 => ([], SpecReposResolution.Refused(
                $"spec {position} names no repository — in a scope of several repositories every spec names "
                + $"exactly the one whose code it changes in repos; state it, from: {allowed}")),
            1 => (named, null),
            _ => ([], SpecReposResolution.SplitNeeded(
                $"spec {position} names {named.Count} repositories ({string.Join(", ", named)}) — a spec "
                + "changes exactly one repository; split it into one spec per repository, the one that "
                + "depends on the other second")),
        };
    }

    // The scope's own spelling of a stated name, so a case slip neither refuses nor survives.
    private static string? InScope(string name, IReadOnlyList<string> scope) =>
        scope.FirstOrDefault(s => string.Equals(s, name.Trim(), StringComparison.OrdinalIgnoreCase));
}
