namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-27-729e: what this installation is running, as the SERVER can state it — its own
/// build, the sandbox-agent build each project spawns, and the database behind both.
/// <para>
/// The dashboard's own release is deliberately absent. It cannot reach here: the findings
/// request names the caller's REVISION and nothing else, and the caller's version is
/// constructed as null on purpose, because a revision is what distinguishes two builds of
/// one release. So the browser renders its own release from the constant its bundle was
/// stamped with, labelled as its own, and this report never claims to know it.
/// </para>
/// <para>
/// 2026-09-20-4981: the catalog is TWO facts, never one. <paramref name="Catalog"/> is the
/// binding this server resolved; <paramref name="EmbeddedCatalogVersion"/> is the floor the
/// binary was built against. They differ by design on three of the four source modes, so
/// reporting the pin alone would be a confident false answer.
/// </para>
/// </summary>
public sealed record InstallationIdentityResponse(
    string? ServerRelease,
    string? ServerRevision,
    IReadOnlyList<SandboxAgentRelease> Agents,
    DatabaseIdentity Database,
    CatalogBinding Catalog,
    string? EmbeddedCatalogVersion);
