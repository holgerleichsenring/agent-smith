using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-933de: a container image reference — &lt;registry&gt;/&lt;repository&gt;[:&lt;tag&gt;][@sha256:&lt;64 hex&gt;].
/// The ONE digest-reference type: whatever deploys, rolls back or renders an image (the c81e series) parses it here and
/// asks for a digest, so a tag-only reference never reaches a manifest.
/// </summary>
public sealed partial record ImageReference(string Registry, string Repository, string? Tag = null, string? Digest = null)
{
    public bool HasDigest => Digest is not null;

    /// <summary>A reference text; with <paramref name="digestRequired"/> one without a sha256 digest is refused.</summary>
    public static ImageReference Parse(string text, bool digestRequired = false)
    {
        var match = Shape().Match(text ?? string.Empty);
        if (!match.Success)
            throw new FormatException($"'{text}' is not an image reference <registry>/<repository>[:<tag>][@sha256:<digest>]");
        var digest = match.Groups["digest"].Success ? match.Groups["digest"].Value : null;
        if (digestRequired && digest is null)
            throw new FormatException($"'{text}' names no sha256 digest; an image is deployed by digest, never by tag alone");
        var tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : null;
        return new ImageReference(match.Groups["registry"].Value, match.Groups["repository"].Value, tag, digest);
    }

    /// <summary>Whether a text is a sha256 digest, 'sha256:' and 64 lower-case hex digits.</summary>
    public static bool IsDigest(string? text) => text is not null && DigestShape().IsMatch(text);

    public ImageReference WithDigest(string digest) => IsDigest(digest)
        ? this with { Digest = digest }
        : throw new FormatException($"'{digest}' is not a sha256 digest");

    public override string ToString() =>
        $"{Registry}/{Repository}{(Tag is null ? "" : ":" + Tag)}{(Digest is null ? "" : "@" + Digest)}";

    [GeneratedRegex(@"^(?<registry>[a-z0-9.-]+(:[0-9]{1,5})?)/(?<repository>[a-z0-9]+([._-]+[a-z0-9]+)*(/[a-z0-9]+([._-]+[a-z0-9]+)*)*)(:(?<tag>[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}))?(@(?<digest>sha256:[a-f0-9]{64}))?$")]
    private static partial Regex Shape();

    [GeneratedRegex("^sha256:[a-f0-9]{64}$")]
    private static partial Regex DigestShape();
}
