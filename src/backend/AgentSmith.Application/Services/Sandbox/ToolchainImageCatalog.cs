namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The convention image a language resolves to when nothing more specific named
/// one. Extracted from SandboxSpecBuilder (p0504) so the resolution chain and the
/// builder each keep one responsibility.
/// <para>
/// 2026-08-25-014d: this table is the ONLY name-driven decision left here. Which
/// registries an image may come from is the operator's, and lives in
/// <see cref="ImageRegistryTrust"/>; whether an image carries git is discovered
/// at the checkout that needs it, not guessed from its tag.
/// </para>
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

    /// <summary>p0194: tests read this to pin every entry against the bases this
    /// repository has confirmed ship git. These are OUR curated values, so the
    /// pin is an assertion about data we control — not a runtime guess about an
    /// image somebody else named.</summary>
    public static IReadOnlyDictionary<string, string> KnownLanguages => LanguageImages;

    /// <summary>The convention image for a language, or null when unknown.</summary>
    public static string? ForLanguage(string? language) =>
        !string.IsNullOrEmpty(language) && LanguageImages.TryGetValue(language, out var image)
            ? image
            : null;
}
