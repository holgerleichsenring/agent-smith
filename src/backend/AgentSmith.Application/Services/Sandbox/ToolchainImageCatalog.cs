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
        //
        // 2026-08-25-3804: an UNVERSIONED name resolves to the highest SDK in this
        // table, because a .NET SDK builds every TFM up to its own and none above
        // it. That is a property of the toolchain, not a claim about the calendar:
        // this table does not know what the newest .NET is and must not pretend to.
        // What keeps it honest is a check, not a comment — ToolchainImageCatalogTests
        // resolves THIS repository's own declared language against THIS repository's
        // own TargetFramework and fails when the image cannot build it. The entry was
        // one major behind for a month, which is exactly how long an unchecked
        // "latest" claim survives. A VERSIONED name still pins what it names.
        ["dotnet8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["dotnet9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["dotnet10"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        ["dotnet"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        [".net"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        [".net 8"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        [".net 9"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        [".net 10"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        ["net8.0"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["net9.0"] = "mcr.microsoft.com/dotnet/sdk:9.0",
        ["net10.0"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        ["csharp"] = "mcr.microsoft.com/dotnet/sdk:10.0",
        ["c#"] = "mcr.microsoft.com/dotnet/sdk:10.0",
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
