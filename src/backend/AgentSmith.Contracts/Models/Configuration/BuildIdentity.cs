namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-08-25-8c97: which build a half of the product is. The REVISION is the identity —
/// the release version cannot be, because it changes only on a release commit while images
/// publish on every push to the trunk, so about nine builds in ten carry the string their
/// predecessor carried and two genuinely different images compare equal. The version rides
/// along because it is what an operator reads and recognises.
/// <para>
/// Both values are stamped into the image at build time and read back from the environment,
/// so an image built by hand carries neither and claims no identity at all — which is why
/// <see cref="DiffersFrom"/> answers false unless BOTH sides know what they are.
/// </para>
/// </summary>
public sealed record BuildIdentity(string? Revision, string? Version)
{
    /// <summary>The build argument and environment variable carrying <see cref="Revision"/>.</summary>
    public const string RevisionVariable = "AGENTSMITH_BUILD_REVISION";

    /// <summary>The build argument and environment variable carrying <see cref="Version"/>.</summary>
    public const string VersionVariable = "AGENTSMITH_RELEASE_VERSION";

    private const int ShortRevisionLength = 12;

    /// <summary>A half that was not stamped says nothing, and silence is not a mismatch.</summary>
    public bool IsKnown => !string.IsNullOrWhiteSpace(Revision);

    /// <summary>The revision as an operator quotes it — the short commit form.</summary>
    public string ShortRevision => Normalized() is { Length: > ShortRevisionLength } full
        ? full[..ShortRevisionLength]
        : Normalized() ?? "unknown";

    /// <summary>The build in one phrase: the release an operator recognises, and the
    /// revision that actually distinguishes two builds of it.</summary>
    public string Display => IsKnown && !string.IsNullOrWhiteSpace(Version)
        ? $"{Version.Trim()} ({ShortRevision})"
        : ShortRevision;

    /// <summary>
    /// Two builds of one release differ when their revisions differ. An unknown identity on
    /// either side is not a difference — it is an absence, and reporting it would turn every
    /// hand-built image and every test run into a false alarm.
    /// </summary>
    public bool DiffersFrom(BuildIdentity other) =>
        IsKnown && other.IsKnown
        && !string.Equals(Normalized(), other.Normalized(), StringComparison.OrdinalIgnoreCase);

    private string? Normalized() => Revision?.Trim();
}
