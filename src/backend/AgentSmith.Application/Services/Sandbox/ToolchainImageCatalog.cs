using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The convention knowledge a toolchain image is judged by: which image a
/// language resolves to, and whether a named image may be trusted to run a
/// sandbox at all. Extracted from SandboxSpecBuilder (p0504) so the resolution
/// chain and the builder each keep one responsibility.
/// </summary>
public static class ToolchainImageCatalog
{
    // Keys cover both ProjectMap.PrimaryLanguage's analyzer output (lowercase
    // canonical: csharp / node / typescript / python / go / rust) AND the
    // operator-facing strings the context.yaml schema documents under stack.lang
    // (C#, .NET 8, TypeScript, JavaScript, Python, Go, Rust). The dictionary is
    // OrdinalIgnoreCase so case variants resolve too. Adding a new language
    // means a row here plus its image — no glue code on call sites.
    private static readonly Dictionary<string, string> LanguageImages = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET / C# family — canonical + operator-facing variants.
        // Bare C#/.NET resolve to the LATEST SDK: the .NET 9 SDK builds every
        // supported TFM (net8.0, net9.0, …), so it is the strictly-safer default
        // for a "C#" project of unknown/mixed target (a solution can mix net8 +
        // net9, as real estates do). Explicit dotnet8/.net 8 still pin 8.0.
        ["dotnet8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["dotnet9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["dotnet"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        [".net"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        [".net 8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        [".net 9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["csharp"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["c#"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        // Node / TS / JS — full bookworm (not -slim) because git must be
        // present in the sandbox: CheckoutSourceHandler runs `git clone`
        // INSIDE the sandbox, and the -slim variants drop git to save ~750MB.
        ["node"] = "node:20-bookworm",
        ["node20"] = "node:20-bookworm",
        ["node.js"] = "node:20-bookworm",
        ["nodejs"] = "node:20-bookworm",
        ["javascript"] = "node:20-bookworm",
        ["typescript"] = "node:20-bookworm",
        // Python — same reason, drop -slim so git is in the image.
        ["python"] = "python:3.12-bookworm",
        ["python3"] = "python:3.12-bookworm",
        // Go
        ["go"] = "golang:1.22-bookworm",
        ["golang"] = "golang:1.22-bookworm",
        // Rust
        ["rust"] = "rust:1.79-bookworm"
    };

    /// <summary>p0194: tests read this to pin every entry against a
    /// git-bearing image allowlist. CheckoutSourceHandler clones inside the
    /// sandbox, so a slim/alpine entry would break checkout silently.</summary>
    public static IReadOnlyDictionary<string, string> KnownLanguages => LanguageImages;

    // p0265: a sandbox runs `git clone` inside the toolchain image
    // (CheckoutSourceHandler), so the image MUST bundle git. These patterns
    // recognise git-bearing tags; a -slim / -alpine / bare tag matches none and
    // is rejected. Single source of truth — the LanguageImages allowlist test
    // (p0194), the LLM-named stack.image validation and the profile image gate
    // (p0504) all read it.
    public static readonly Regex[] GitBearingImagePatterns =
    [
        // Microsoft .NET SDK images include git in every tag.
        new(@"^mcr\.microsoft\.com/dotnet/sdk:", RegexOptions.Compiled),
        // Debian bookworm full base bundles git.
        new(@":[^-]*-bookworm$", RegexOptions.Compiled),
        // Debian bullseye full base bundles git.
        new(@":[^-]*-bullseye$", RegexOptions.Compiled),
        // The -scm suffix on buildpack-deps is explicitly source-control-tooling.
        new(@"^buildpack-deps:[^-]+-scm$", RegexOptions.Compiled),
    ];

    /// <summary>Does this image carry git, judged by its tag?</summary>
    public static bool IsGitBearing(string image) =>
        GitBearingImagePatterns.Any(p => p.IsMatch(image));

    // p0265: trusted registries an LLM-named stack.image may pull from. A
    // supply-chain boundary (feedback_safety_in_api_not_process): the image
    // string is LLM-authored, so we only accept official Microsoft, GitHub
    // Container Registry, or Docker Hub *official library* images (single repo
    // segment, no user namespace). Anything else falls back to the language table.
    public static bool IsTrustedRegistry(string image)
    {
        if (image.StartsWith("mcr.microsoft.com/", StringComparison.Ordinal)) return true;
        if (image.StartsWith("ghcr.io/", StringComparison.Ordinal)) return true;
        // Docker Hub official "library" image: the repository part (before the
        // tag) has no '/', e.g. node:20-bookworm, buildpack-deps:bookworm-scm.
        // user/repo or other-registry.tld/... both contain a '/' and are rejected.
        var repo = image.Split(':', 2)[0];
        return !repo.Contains('/', StringComparison.Ordinal);
    }

    /// <summary>The convention image for a language, or null when unknown.</summary>
    public static string? ForLanguage(string? language) =>
        !string.IsNullOrEmpty(language) && LanguageImages.TryGetValue(language, out var image)
            ? image
            : null;
}
