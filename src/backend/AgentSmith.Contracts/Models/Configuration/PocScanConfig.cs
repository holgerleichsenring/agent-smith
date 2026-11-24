namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933dc: how a PoC upload is scanned before it is committed, from <c>pocs.scan:</c>; an absent block reads
/// as these defaults. The image stage of 933dd reads <see cref="UnfixedCriticalBlock"/> and
/// <see cref="SecretMinConfidence"/> too, so both stages decide alike.
/// </summary>
public sealed partial class PocScanConfig
{
    public const string OsvImageKey = "pocs.scan.osv_image";
    public const string TimeoutSecondsKey = "pocs.scan.timeout_seconds";
    public const string UnfixedCriticalBlockKey = "pocs.scan.unfixed_critical_block";
    public const string SecretMinConfidenceKey = "pocs.scan.secret_min_confidence";
    public const string TrivyImageKey = "pocs.scan.trivy_image";

    private const int LowestConfidence = 1; // not a limit: the catalog rates pattern confidence from 1
    private const int HighestConfidence = 10; // not a limit: the catalog rates pattern confidence up to 10

    /// <summary>The OSV-Scanner image, pinned by digest ('…@sha256:&lt;64 hex&gt;'). No default.</summary>
    public string OsvImage { get; set; } = string.Empty;

    /// <summary>How long the dependency scan may take, the database download included.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>A critical vulnerability with no fixed version blocks (and is overrulable) like one with a fix.</summary>
    public bool UnfixedCriticalBlock { get; set; } = true;

    /// <summary>A 'secrets' pattern of severity high or critical blocks at this confidence or above.</summary>
    public int SecretMinConfidence { get; set; } = 8;

    /// <summary>2026-11-23-933dd: the Trivy image the build pipeline's scan job runs, pinned by digest. No default.</summary>
    public string TrivyImage { get; set; } = string.Empty;

    /// <summary>Whether <see cref="OsvImage"/> names an image by digest.</summary>
    public bool IsPinned() => Pinned(OsvImage);

    private static bool Pinned(string image) => image.Contains("@sha256:", StringComparison.Ordinal);

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason.</summary>
    public IEnumerable<string> Problems(bool enabled)
    {
        if (enabled && string.IsNullOrWhiteSpace(OsvImage))
            yield return $"{OsvImageKey} is empty while pocs.enabled is true; name the OSV-Scanner image pinned by digest, "
                + "because a dependency scan that cannot run would read as clean";
        else if (!string.IsNullOrWhiteSpace(OsvImage) && !IsPinned())
            yield return $"{OsvImageKey} is '{OsvImage}'; it must name the image by digest ('…@sha256:…'), because a tag can "
                + "be moved to another image";
        foreach (var problem in TrivyProblems(enabled)) yield return problem;
        if (TimeoutSeconds < 1)
            yield return $"{TimeoutSecondsKey} is {TimeoutSeconds}; it must be 1 or more, because 0 or less would fail every scan";
        if (SecretMinConfidence is < LowestConfidence or > HighestConfidence)
            yield return $"{SecretMinConfidenceKey} is {SecretMinConfidence}; it must be from {LowestConfidence} to "
                + $"{HighestConfidence}, because the catalog rates pattern confidence from 1 to 10";
    }

    private IEnumerable<string> TrivyProblems(bool enabled)
    {
        if (enabled && string.IsNullOrWhiteSpace(TrivyImage))
            yield return $"{TrivyImageKey} is empty while pocs.enabled is true; name the Trivy image pinned by digest, because "
                + "the build pipeline's image scan runs it";
        else if (!string.IsNullOrWhiteSpace(TrivyImage) && !PinnedImage().IsMatch(TrivyImage))
            yield return $"{TrivyImageKey} is '{TrivyImage}'; it must name the image by digest ('…@sha256:…'), because a tag can "
                + "be moved to another image";
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9._/:-]+@sha256:[0-9a-f]{64}$")]
    private static partial System.Text.RegularExpressions.Regex PinnedImage();
}
