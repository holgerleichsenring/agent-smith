namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5a: the WIQL clause matching work item ids that BEGIN with a typed number.
/// <para>
/// There is no LIKE in WIQL, and its substring operator is defined only on text fields while
/// System.Id is an integer — this tree records that refusal in its own discovery builder. What the
/// language does have is comparison, so a prefix is the ranges it stands for.
/// </para>
/// </summary>
internal static class AzureDevOpsIdPrefixClause
{
    public static string For(string prefix) =>
        "(" + string.Join(" OR ", TicketNumberPrefix.Ranges(prefix).Select(Clause)) + ")";

    private static string Clause((long From, long To) range) =>
        range.From == range.To
            ? $"[System.Id] = {range.From}"
            : $"([System.Id] >= {range.From} AND [System.Id] <= {range.To})";
}
