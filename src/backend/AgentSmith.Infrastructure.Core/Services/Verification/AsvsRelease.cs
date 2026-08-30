namespace AgentSmith.Infrastructure.Core.Services.Verification;

/// <summary>
/// 2026-08-30-0ea8: THE ONE PLACE the ingested standard's release is named.
/// <para>
/// The checked-in export carries no version in its file name and the build pins its
/// digest, not its release, so this constant is the only statement of which release the
/// product ships. Naming it once is what lets a later phase turn the source into a port
/// with a rename instead of a refactor — and it is why the attribution below is composed
/// from the tag rather than repeating it.
/// </para>
/// <para>
/// The tag is the upstream's IMMUTABLE release. Its moving sibling publishes assets under
/// identical names, so a pin that names the moving one pins nothing.
/// </para>
/// </summary>
internal static class AsvsRelease
{
    /// <summary>The upstream release tag the checked-in export was taken from.</summary>
    public const string Tag = "v5.0.0_release";

    private const string Title = "OWASP Application Security Verification Standard";
    private const string Licence = "CC BY-SA 4.0";
    private const string Home = "https://github.com/OWASP/ASVS";

    /// <summary>
    /// The line that travels with the ingested text wherever it is quoted. The standard
    /// is licensed for commercial use and redistribution, and requires attribution and a
    /// licence notice in return; see NOTICE in the repository root.
    /// </summary>
    public const string Attribution =
        $"Requirement text: {Title} ({Tag}), (c) OWASP Foundation, licensed under {Licence} — {Home}";
}
