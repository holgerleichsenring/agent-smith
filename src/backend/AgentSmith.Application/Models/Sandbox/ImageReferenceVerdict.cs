namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-10-12-50bdb: whether an image exists in its registry. <see cref="Detail"/> says what
/// the registry said, or why it could not be asked; <see cref="Candidates"/> are the tags of
/// the same repository closest to a missing one, and <see cref="TagsListed"/> says whether
/// the tag list could be read at all.
/// </summary>
public sealed record ImageReferenceVerdict(
    ImageReferenceState State, string Image, string Detail,
    IReadOnlyList<string> Candidates, bool TagsListed)
{
    public static ImageReferenceVerdict Exists(string image) =>
        new(ImageReferenceState.Exists, image, string.Empty, [], false);

    public static ImageReferenceVerdict Unverified(string image, string detail) =>
        new(ImageReferenceState.Unverified, image, detail, [], false);

    /// <summary><paramref name="candidates"/> is null when the tag list could not be read.</summary>
    public static ImageReferenceVerdict Missing(
        string image, string detail, IReadOnlyList<string>? candidates) =>
        new(ImageReferenceState.Missing, image, detail, candidates ?? [], candidates is not null);
}
