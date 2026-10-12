namespace AgentSmith.Application.Services.Repositories;

/// <summary>
/// 2026-10-12-f5f7c: which repository a tool call is about — the one rule for a tool's optional
/// <c>repository</c> argument. A named repository must be one of the run's; an unnamed one is
/// the run's only repository, and with more than one there is nothing to default to, so the call
/// says which. Exactly one of the two is set when the run has repositories; both are null when
/// it has none.
/// </summary>
public sealed record RunRepositoryPick(string? Name, string? Error)
{
    public static RunRepositoryPick From(IReadOnlyList<string> names, string? named)
    {
        if (!string.IsNullOrWhiteSpace(named))
            return names.FirstOrDefault(n => string.Equals(n, named.Trim(), StringComparison.OrdinalIgnoreCase))
                is { } known
                ? new RunRepositoryPick(known, null)
                : new RunRepositoryPick(null,
                    $"Error: unknown repository '{named}'. This run's repositories: [{string.Join(", ", names)}].");
        return names.Count switch
        {
            0 => new RunRepositoryPick(null, null),
            1 => new RunRepositoryPick(names[0], null),
            _ => new RunRepositoryPick(null,
                $"Error: this run has {names.Count} repositories — pass repository= one of " +
                $"[{string.Join(", ", names)}], the one this is about."),
        };
    }
}
