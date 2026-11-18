using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: what a deployable version looks like — a tag or a commit, letters, digits, '.', '_' and '-',
/// starting with a letter or digit. It is written into a manifest and named in a branch, so a digest
/// (<c>sha256:…</c>) is refused: ':' is not a character a git ref may carry.
/// </summary>
public static partial class DeployVersionShape
{
    public const string Description = "letters, digits, '.', '_' and '-', starting with a letter or a digit";

    public static bool IsValid(string? version) => version is not null && Pattern().IsMatch(version);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex Pattern();
}
