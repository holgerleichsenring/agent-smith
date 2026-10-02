namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: the WHERE prefix that scopes a WIQL query to one team project and to the
/// states an operator calls open. Shared by the discovery lister and the ticket search so the
/// two cannot drift apart on what "open" means.
/// <para>
/// THE DEFAULT IS A GUESS AND IT IS WORTH SAYING SO. New, Active and Committed mix two process
/// templates and name no Resolved, Proposed or Approved — so on an unconfigured Agile or CMMI
/// project some genuinely open work is out of reach of both readers. The cure is configuration,
/// <c>open_states</c> on the tracker; what neither reader will do is return a closed ticket.
/// </para>
/// </summary>
internal static class AzureDevOpsOpenScope
{
    private static readonly string[] Default = ["New", "Active", "Committed"];

    public static IReadOnlyList<string> States(IReadOnlyList<string>? configured) =>
        configured is { Count: > 0 } ? configured : Default;

    /// <summary>2026-09-28-1da5a: the team project alone. A number-prefix search is scoped but not
    /// state-filtered — see the search's own decision.</summary>
    public static string Project(string project) =>
        $"[System.TeamProject] = '{Escaped(project)}'";

    public static string Where(string project, IReadOnlyList<string>? configured) =>
        $"[System.TeamProject] = '{Escaped(project)}' "
        + $"AND [System.State] IN ({string.Join(", ", States(configured).Select(s => $"'{Escaped(s)}'"))})";

    /// <summary>A WIQL string literal escapes its delimiter by doubling it.</summary>
    public static string Escaped(string value) => value.Replace("'", "''");
}
