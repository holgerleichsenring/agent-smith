namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: where one ticket's spec set lies — the path segment under <see cref="Root"/>.
/// 2026-10-06-03c7c: the ticket's IDENTITY is <see cref="TicketKey"/>; this type only derives a
/// path from it. The ticket key stays in the path even though the ticket branch already carries
/// it: after a merge the specs of many tickets coexist in the trunk and a fixed path would
/// overwrite.
/// </summary>
public readonly record struct SpecSetKey(string Value)
{
    /// <summary>Repo-relative root under which every ticket's spec directory lives.</summary>
    public const string Root = ".agentsmith/specs";

    /// <summary>The path a ticket's set lies under.</summary>
    public static SpecSetKey For(TicketKey ticket) => new(ticket.Value);

    /// <summary>Repo-relative directory holding the phase specs and the run record.</summary>
    public string Directory => $"{Root}/{Value}";

    /// <summary>Repo-relative path of one phase's schema-valid spec.</summary>
    public string YamlPath(string fileStem) => $"{Directory}/{fileStem}.yaml";

    /// <summary>
    /// Repo-relative path of one phase's markdown companion — the carrier of the
    /// verbatim ticket spans. Phase specs were deliberately built to avoid code; a
    /// migration manual's value IS its code, and the split keeps both properties.
    /// </summary>
    public string MarkdownPath(string fileStem) => $"{Directory}/{fileStem}.md";

    /// <summary>Repo-relative path of the derivation's accounting, readable in the PR.</summary>
    public string AccountingPath => $"{Directory}/accounting.md";

    public override string ToString() => Value;
}
