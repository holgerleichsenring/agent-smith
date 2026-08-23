namespace AgentSmith.Server.Security;

/// <summary>
/// p0503a: the closed catalog of capabilities a caller can hold. The PERMISSION is the
/// unit, never the role — a route needs <c>runs.control</c>, not "operator", so an
/// installation with a different org chart is a configuration line rather than a code
/// change to the route table.
/// <para>
/// Every name here was derived from the mapped routes, not invented.
/// <see cref="IdentityRead"/> is the one exception and is deliberate: no route carries it
/// yet, it is what the identity endpoint of *a token is validated against one authority*
/// will need, and cataloguing it here means that phase adds a route instead of reopening
/// the catalog.
/// </para>
/// </summary>
internal static class Permissions
{
    internal const string RunsRead = "runs.read";
    internal const string RunsControl = "runs.control";
    internal const string RunsDelete = "runs.delete";
    internal const string ProjectsInit = "projects.init";
    internal const string CatalogRead = "catalog.read";
    internal const string DiagnosticsRead = "diagnostics.read";

    // A probe makes an outbound AUTHENTICATED call into a customer system with the
    // installation's own credentials. That is an action, not a read.
    internal const string DiagnosticsProbe = "diagnostics.probe";

    internal const string ConfigRead = "config.read";
    internal const string ConfigWrite = "config.write";
    internal const string ConfigExport = "config.export";
    internal const string ConfigImport = "config.import";

    // The secret entity's own CRUD, plus the four routes that cross into it.
    internal const string SecretsRead = "secrets.read";
    internal const string SecretsWrite = "secrets.write";

    internal const string IdentityRead = "identity.read";

    internal static IReadOnlyList<string> All { get; } =
    [
        RunsRead, RunsControl, RunsDelete, ProjectsInit, CatalogRead,
        DiagnosticsRead, DiagnosticsProbe, ConfigRead, ConfigWrite, ConfigExport,
        ConfigImport, SecretsRead, SecretsWrite, IdentityRead,
    ];
}
