namespace AgentSmith.Server.Security;

/// <summary>
/// p0503a: the roles agent-smith ships (2026-11-23-933da adds poc), expressed as BUNDLES over
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

    /// <summary>2026-11-23-933da: a person who hosts PoCs and nothing else — their own poc- project's binding decides
    /// which PoCs they see.</summary>
    internal const string Poc = "poc";

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
        // 2026-09-15-9033: an operator holds the spec dialog, because it files tickets
        // against this installation's trackers. A reader does not — and the hub the
        // dialog is delivered through already demands runs.read, which the operator
        // bundle carries through reader.
        Permissions.DialogWrite,
        // 2026-11-23-933da: an operator opens the PoCs area and reads every PoC through their global runs.read; acting
        // on PoCs stays pocs.write and pocs.admin.
        Permissions.PocsRead,
    ];

    private static readonly string[] PocBundle = [Permissions.PocsRead, Permissions.PocsWrite, Permissions.IdentityRead];

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> All { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Admin] = Permissions.All,
            [Operator] = OperatorBundle,
            [Reader] = ReaderBundle,
            [Poc] = PocBundle,
        };
}
