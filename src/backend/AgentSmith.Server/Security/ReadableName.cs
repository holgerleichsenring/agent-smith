namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-09-27-481bd: whether the claim an installation names its callers by is a READABLE one.
/// <para>
/// The identity carries the name-claim value as its subject, falling back to the opaque one when
/// the directory sent none — so a name is always present and is not always a name. The default is
/// <c>sub</c>, which most installations never change, and greeting somebody by a directory
/// identifier is worse than not greeting them at all.
/// </para>
/// <para>
/// ONE RULE, TWO READERS. The access view already computed this inline for its own page; a second
/// copy on the identity would have been the second shape that eventually disagrees. What this
/// detects is the DEFAULT and not opacity itself: an installation pointing the claim at an object
/// id, or at a username that happens to be a directory identifier, passes — claiming more would be
/// a promise about other people's directories.
/// </para>
/// </summary>
internal static class ReadableName
{
    private const string OpaqueSubjectClaim = "sub";

    public static bool Is(string nameClaim) =>
        !string.Equals(nameClaim, OpaqueSubjectClaim, StringComparison.Ordinal);
}
