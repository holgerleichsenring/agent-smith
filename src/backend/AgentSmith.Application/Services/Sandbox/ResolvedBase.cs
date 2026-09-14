namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-5cdf: the ONE base a repository's ladder resolved — the feature rung a
/// slice belongs under, or the clone's own base when no rung exists.
/// <para>
/// Three consumers ask "what is this branch's base": the work-branch cut, the base merge
/// and the delivery diff. Each answering for itself is how a run cuts from one branch and
/// accounts against another — the slice would then report its predecessors' work as its
/// own delivery, to the keystone, to the account and to the pull request. One answer
/// travels to all three instead.
/// </para>
/// <para>
/// <see cref="FellThrough"/> is the report 2026-08-25-0eae requires: a ladder that reached
/// its bottom says so rather than presenting the clone's base as a rung it found.
/// </para>
/// </summary>
/// <param name="Name">The short branch name without the <c>origin/</c> prefix, or null
/// when the clone names no base at all.</param>
/// <param name="FellThrough">True when no rung existed and this is the clone's own base.</param>
public sealed record ResolvedBase(string? Name, bool FellThrough)
{
    /// <summary>A rung that exists in this clone.</summary>
    public static ResolvedBase Rung(string name) => new(name, FellThrough: false);

    /// <summary>What every run resolved before a rung existed anywhere.</summary>
    public static ResolvedBase CloneBase(string? name) => new(name, FellThrough: true);

    /// <summary>The remote-tracking ref to compare, merge or check out, or null when
    /// the ladder found nothing to name.</summary>
    public string? Ref =>
        string.IsNullOrWhiteSpace(Name) ? null : $"origin/{Name}";
}
