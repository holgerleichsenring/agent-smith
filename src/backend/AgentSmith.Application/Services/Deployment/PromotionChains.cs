namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863h: a template's promotions must end in an entry environment. An environment that promotes from
/// itself, or two that promote from each other, would deploy one another forever once promotion is automatic, so the
/// declaration is refused naming the cycle.
/// </summary>
internal static class PromotionChains
{
    public static string? Cycle(IReadOnlyList<DeclaredEnvironment> environments)
    {
        var from = environments.ToDictionary(e => e.Name, e => e.PromotesFrom, StringComparer.OrdinalIgnoreCase);
        foreach (var start in environments)
        {
            var path = new List<string> { start.Name };
            for (var at = start.PromotesFrom; at is not null && from.ContainsKey(at); at = from[at])
            {
                path.Add(at);
                if (string.Equals(at, start.Name, StringComparison.OrdinalIgnoreCase))
                    return $"environments promote in a cycle ({string.Join(" <- ", path)}); promotion has to start at an entry environment";
                if (path.Count > environments.Count) break;
            }
        }
        return null;
    }
}
