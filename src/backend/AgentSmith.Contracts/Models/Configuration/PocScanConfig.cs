namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933dc: how a PoC upload is scanned before it is committed, from <c>pocs.scan:</c>; an absent block reads
/// as these defaults. The image stage of 933dd reads <see cref="UnfixedCriticalBlock"/> and
/// <see cref="SecretMinConfidence"/> too, so both stages decide alike. 2026-11-23-933dg (operator ruling): both images
/// have a verified digest-pinned default; an operator sets a key only to run an own mirror, and empty means the default.
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

    /// <summary>2026-11-23-933dg: OSV-Scanner v2.2.4, its index digest verified against ghcr.io.</summary>
    public const string DefaultOsvImage =
        "ghcr.io/google/osv-scanner:v2.2.4@sha256:f7ba4be68bac8086b1f88fd598fdca1ca67239c79ad2c2b5c78e03a82e5187c4";

    /// <summary>2026-11-23-933dg: Trivy 0.76.2, its index digest verified against Docker Hub.</summary>
    public const string DefaultTrivyImage =
        "aquasec/trivy:0.76.2@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb";

    /// <summary>An own mirror of the OSV-Scanner image, pinned by digest ('…@sha256:&lt;64 hex&gt;'); empty runs
    /// <see cref="DefaultOsvImage"/>.</summary>
    public string OsvImage { get; set; } = string.Empty;

    /// <summary>How long the dependency scan may take, the database download included.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>A critical vulnerability with no fixed version blocks (and is overrulable) like one with a fix.</summary>
    public bool UnfixedCriticalBlock { get; set; } = true;

    /// <summary>A 'secrets' pattern of severity high or critical blocks at this confidence or above.</summary>
    public int SecretMinConfidence { get; set; } = 8;

    /// <summary>2026-11-23-933dd: the Trivy image the build pipeline's scan job runs, pinned by digest; 2026-11-23-933dg:
    /// empty runs <see cref="DefaultTrivyImage"/>.</summary>
    public string TrivyImage { get; set; } = string.Empty;

    /// <summary>2026-11-23-933dg: the OSV-Scanner image a scan runs — the key's value, or the default. A method, so the
    /// settings document never carries it.</summary>
    public string OsvImageInUse() => string.IsNullOrWhiteSpace(OsvImage) ? DefaultOsvImage : OsvImage.Trim();

    /// <summary>2026-11-23-933dg: the Trivy image the pipeline is written with — the key's value, or the default.</summary>
    public string TrivyImageInUse() => string.IsNullOrWhiteSpace(TrivyImage) ? DefaultTrivyImage : TrivyImage.Trim();

    /// <summary>Whether the OSV-Scanner image in use names an image by digest.</summary>
    public bool IsPinned() => Pinned(OsvImageInUse());

    private static bool Pinned(string image) => image.Contains("@sha256:", StringComparison.Ordinal);

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason.</summary>
    public IEnumerable<string> Problems()
    {
        if (!string.IsNullOrWhiteSpace(OsvImage) && !IsPinned())
            yield return $"{OsvImageKey} is '{OsvImage}'; it must name the image by digest ('…@sha256:…'), because a tag can "
                + "be moved to another image";
        foreach (var problem in TrivyProblems()) yield return problem;
        if (TimeoutSeconds < 1)
            yield return $"{TimeoutSecondsKey} is {TimeoutSeconds}; it must be 1 or more, because 0 or less would fail every scan";
        if (SecretMinConfidence is < LowestConfidence or > HighestConfidence)
            yield return $"{SecretMinConfidenceKey} is {SecretMinConfidence}; it must be from {LowestConfidence} to "
                + $"{HighestConfidence}, because the catalog rates pattern confidence from 1 to 10";
    }

    private IEnumerable<string> TrivyProblems()
    {
        if (!string.IsNullOrWhiteSpace(TrivyImage) && !PinnedImage().IsMatch(TrivyImage.Trim()))
            yield return $"{TrivyImageKey} is '{TrivyImage}'; it must name the image by digest ('…@sha256:…'), because a tag can "
                + "be moved to another image";
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9._/:-]+@sha256:[0-9a-f]{64}$")]
    private static partial System.Text.RegularExpressions.Regex PinnedImage();
}
