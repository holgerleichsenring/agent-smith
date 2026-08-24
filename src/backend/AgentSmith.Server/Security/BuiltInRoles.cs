namespace AgentSmith.Server.Security;

/// <summary>
/// p0503a: the three roles agent-smith ships, expressed as BUNDLES over
/// <see cref="Permissions"/>. A role is a convenience for whoever configures the
/// installation; nothing in the route table names one, so a customer whose org chart
/// does not fit these three re-bundles the catalog instead of editing routes.
/// <para>
/// reader sees what the agent DID — runs, the catalog it ran against, the connection
/// snapshot, and who it thinks the caller is. It holds no <c>config.read</c>, because
/// the configuration is where the credentials, the trackers and the repositories are
/// named, and "may look at the run list" is not "may read the installation".
/// </para>
/// </summary>
internal static class BuiltInRoles
{
    internal const string Admin = "admin";
    internal const string Operator = "operator";
    internal const string Reader = "reader";

    private static readonly string[] ReaderBundle =
    [
        Permissions.RunsRead, Permissions.RunsWatch, Permissions.CatalogRead,
        Permissions.DiagnosticsRead, Permissions.IdentityRead,
    ];

    private static readonly string[] OperatorBundle =
    [
        .. ReaderBundle,
        Permissions.RunsControl, Permissions.RunsDelete,
        Permissions.ProjectsInit, Permissions.DiagnosticsProbe,
    ];

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> All { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Admin] = Permissions.All,
            [Operator] = OperatorBundle,
            [Reader] = ReaderBundle,
        };
}
