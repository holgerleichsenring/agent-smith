namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-08-25-0d01: which sandbox-agent tag a project gets, and how that was decided.
/// <para>
/// The accidental mismatch is REMOVED and the deliberate one is JUDGED. Two independently
/// declarable versions that must match are the configuration bug — so when nobody declares
/// one, the tag is derived from the release the server itself is, and there is nothing left
/// to forget to move. Removing the class does not mean forbidding the override: an
/// air-gapped mirror and a bisecting developer both need it. It means the override is now
/// the ONLY way to get a difference, and a difference that was chosen can be reported.
/// </para>
/// </summary>
/// <param name="Version">The tag the sandbox-agent image will be pulled by.</param>
/// <param name="ServerVersion">The release this server is, when its image stamped one.</param>
/// <param name="IsPinned">True when an operator declared the version rather than inheriting it.</param>
public sealed record AgentVersionChoice(string Version, string? ServerVersion, bool IsPinned)
{
    /// <summary>
    /// A pin that names a release other than this server's. Never says "incompatible":
    /// whether the two can talk is a property of the wire protocol between them, which the
    /// live channel reports on, and a tag is not evidence about a protocol. A pin equal to
    /// the derived value, or a server that never learned its own release, is not a difference.
    /// </summary>
    public bool DiffersFromServer =>
        IsPinned
        && !string.IsNullOrWhiteSpace(ServerVersion)
        && !string.Equals(Version.Trim(), ServerVersion!.Trim(), StringComparison.OrdinalIgnoreCase);
}
