namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-01-283db: an uploaded set after a check — the files to store, or why none of them is
/// stored. A refusal names the file and the limit; there is no partial outcome.
/// 2026-10-02-0d72: and the paths left out because they are not what a website is made of — a
/// LICENSE, a .gitignore, a sitemap.xml. Skipping one is not a partial outcome: it was never a
/// candidate for the set.
/// </summary>
public sealed record ReferenceSetCheck(
    IReadOnlyList<ReferenceUploadPart> Files, string? Refusal, IReadOnlyList<string>? SkippedPaths = null)
{
    public static ReferenceSetCheck Refused(string why) => new([], why);

    public bool IsRefused => Refusal is not null;

    public IReadOnlyList<string> Skipped => SkippedPaths ?? [];
}
