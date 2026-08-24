namespace AgentSmith.Server.Security;

/// <summary>
/// p0503d: the way back in. A comma-separated list in <c>AGENTSMITH_ADMIN_GRANT</c> names
/// the callers who hold <see cref="BuiltInRoles.Admin"/> whatever the directory says, so an
/// installation cannot lock every human out of the surface that fixes the mapping. It
/// reaches no editable surface — an environment variable is changed where the deployment
/// is, which is the point.
/// <para>
/// Every entry is PREFIXED, <c>group:</c> or <c>sub:</c>, and matched only against the
/// claim its prefix names. A grant matched across claim types is a privilege-escalation
/// surface: an attacker-controllable email claim colliding with a group identifier would
/// be admin. An unprefixed entry is therefore refused rather than tried everywhere.
/// </para>
/// </summary>
internal sealed class AdminGrant
{
    public const string EnvVar = "AGENTSMITH_ADMIN_GRANT";

    private const string GroupPrefix = "group:";
    private const string SubjectPrefix = "sub:";

    private readonly List<string> _groups = [];
    private readonly List<string> _subjects = [];
    private readonly List<string> _findings = [];

    public AdminGrant(Func<string, string?> environment)
    {
        foreach (var entry in Entries(environment(EnvVar)))
        {
            if (entry.StartsWith(GroupPrefix, StringComparison.Ordinal))
                _groups.Add(entry[GroupPrefix.Length..].TrimStart('/'));
            else if (entry.StartsWith(SubjectPrefix, StringComparison.Ordinal))
                _subjects.Add(entry[SubjectPrefix.Length..]);
            else Refuse(entry);
        }
    }

    /// <summary>What is wrong with the grant as written, said where an operator can read it.</summary>
    public IReadOnlyList<string> Findings => _findings;

    /// <summary>Whether this caller is one of the named ones. Ordinal — both are opaque identifiers.</summary>
    public bool Holds(IEnumerable<string> groupValues, string? subject) =>
        groupValues.Any(group => _groups.Contains(group.TrimStart('/'), StringComparer.Ordinal))
        || (subject is not null && _subjects.Contains(subject, StringComparer.Ordinal));

    private void Refuse(string entry) => _findings.Add(
        $"The admin grant entry '{entry}' names no claim, so it grants nothing. Write "
        + $"'{GroupPrefix}<value>' or '{SubjectPrefix}<value>' — a grant matched against "
        + "whichever claim happens to contain the value is a way in nobody intended.");

    private static IEnumerable<string> Entries(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
