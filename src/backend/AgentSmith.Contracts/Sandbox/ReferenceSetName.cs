namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-10-01-283db: what an uploaded website is called — the folder all its paths share, which a
/// folder upload or an unpacked archive puts every file under, or <see cref="Unnamed"/>.
/// 2026-10-01-283df: moved here from the set repository, because a run names the sets it carries
/// from the same paths and two derivations of one name would be two answers.
/// </summary>
public static class ReferenceSetName
{
    /// <summary>The name a set whose paths share no folder is listed under.</summary>
    public const string Unnamed = "site";

    public static string Of(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var tops = paths.Select(p => p.IndexOf('/') is var slash and > 0 ? p[..slash] : null).Distinct().ToList();
        return tops is [{ } only] ? only : Unnamed;
    }
}
