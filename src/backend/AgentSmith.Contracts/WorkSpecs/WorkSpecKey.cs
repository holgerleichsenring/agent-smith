namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: identity of a work spec — <c>&lt;provider&gt;-&lt;ticketId&gt;</c>. The same
/// value is the path segment under <see cref="Root"/> and the database key.
/// The ticket id stays in the path even though the branch already carries it:
/// after a merge the specs of many tickets coexist in the trunk and a fixed
/// path would overwrite.
/// </summary>
public readonly record struct WorkSpecKey(string Value)
{
    /// <summary>Repo-relative root under which every ticket's spec directory lives.</summary>
    public const string Root = ".agentsmith/specs";

    private const string TicketsRoot = Root + "/tickets";

    public static WorkSpecKey For(string provider, string ticketId) =>
        new($"{Slug(provider)}-{Slug(ticketId)}");

    /// <summary>Repo-relative directory holding spec.yaml and spec.md.</summary>
    public string Directory => $"{TicketsRoot}/{Value}";

    /// <summary>Repo-relative path of the typed artifact.</summary>
    public string SpecPath => $"{Directory}/spec.yaml";

    /// <summary>Repo-relative path of the authored companion carrying the samples.</summary>
    public string SamplesPath => $"{Directory}/spec.md";

    public override string ToString() => Value;

    private static string Slug(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";
        var chars = raw.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        return new string(chars).Trim('-') is { Length: > 0 } s ? s : "unknown";
    }
}
