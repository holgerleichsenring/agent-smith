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

    // p0503c: the LIVE sandbox drawer, which is a cross-viewer mutation rather than a
    // read: the expansion refcount is process-global, so one viewer collapsing turns the
    // fan-out off for another. A reader holds it by default, because the live drawer is
    // most of what watching a run is for; it is separable so an installation can withhold
    // the mutation without withholding the run view.
    internal const string RunsWatch = "runs.watch";
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

    // 2026-08-28-3793: a whole-database archive is not a configuration read. It carries
    // ticket text, prompts, artifacts and the config store's secrets in clear, so whoever
    // may take one may read everything this installation has ever done — a different grant
    // from reading or editing the configuration, and stated as its own. Export and import
    // are separable for the reason the config pair is: taking a copy and replacing the
    // database are not the same act.
    internal const string ArchiveExport = "archive.export";
    internal const string ArchiveImport = "archive.import";

    // 2026-08-26-7a51: the access surface decides its own permission instead of inheriting
    // config.write. A custom role bundling config.write is legal, and the settings route
    // that used to carry the role mapping would have let such a caller grant themselves
    // admin — and with it secrets.read and secrets.write, which this catalog deliberately
    // kept separable.
    internal const string AccessRead = "access.read";
    internal const string AccessWrite = "access.write";

    internal static IReadOnlyList<string> All { get; } =
    [
        RunsRead, RunsWatch, RunsControl, RunsDelete, ProjectsInit, CatalogRead,
        DiagnosticsRead, DiagnosticsProbe, ConfigRead, ConfigWrite, ConfigExport,
        ConfigImport, SecretsRead, SecretsWrite, IdentityRead, AccessRead, AccessWrite,
        ArchiveExport, ArchiveImport,
    ];
}
