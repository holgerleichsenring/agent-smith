using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7a: what a cut that cannot be trusted becomes in a scope of several repositories —
/// the ticket handed back, naming them. The whole-ticket fallback would put the whole ticket into a
/// spec that names one repository, and any one is a guess; only a person can say how the work
/// divides between them. 2026-10-12-d142: a <see cref="SpecHandbackCase.ScopeUnassigned"/> — it parks
/// where a person can answer and a re-trigger after a reply cuts again; nothing contradicts the code.
/// </summary>
public static class SpecScopeHandback
{
    public static SpecSet For(string key, string series, SpecSource source, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var names = string.Join(", ", scope);
        var reason = "The ticket could not be cut into specs that each change exactly one of the "
            + $"repositories in scope ({names}), and putting the whole ticket into one of them would be "
            + "a guess. Say which repository each part of the work belongs to, or split the ticket "
            + "per repository, and trigger it again.";
        return new SpecSet(
            key, [], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            source,
            new SpecHandback(SpecHandbackCase.ScopeUnassigned, reason),
            Series: series);
    }
}
