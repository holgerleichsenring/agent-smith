using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Services.Security.Code;

/// <summary>
/// Regex patterns matching route declarations in .NET, Express, FastAPI and Spring source.
/// Each pattern captures the HTTP method and path template, and is scoped to file
/// extensions to keep per-language patterns from cross-matching one another.
/// </summary>
internal static class FrameworkRoutePatterns
{
    private static readonly string[] DotNetExt = [".cs"];
    private static readonly string[] JsTsExt = [".js", ".ts", ".mjs", ".cjs"];
    private static readonly string[] PythonExt = [".py"];
    private static readonly string[] JavaKotlinExt = [".java", ".kt"];

    public static readonly IReadOnlyList<FrameworkRoutePattern> All =
    [
        new("dotnet", new Regex(
            @"\[Http(Get|Post|Put|Delete|Patch)\s*\(\s*""(?<path>[^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            DotNetExt),

        new("dotnet", new Regex(
            @"app\.Map(Get|Post|Put|Delete|Patch)\s*\(\s*""(?<path>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            DotNetExt),

        new("express", new Regex(
            @"\b(?:router|app)\s*\.\s*(get|post|put|delete|patch)\s*\(\s*[""'`](?<path>[^""'`]+)[""'`]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            JsTsExt),

        new("fastapi", new Regex(
            @"@\s*(?:app|router)\s*\.\s*(get|post|put|delete|patch)\s*\(\s*[""'](?<path>[^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            PythonExt),

        new("spring", new Regex(
            @"@(Get|Post|Put|Delete|Patch)Mapping\s*\(\s*(?:value\s*=\s*)?[""'](?<path>[^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            JavaKotlinExt),
    ];
}

internal sealed record FrameworkRoutePattern(
    string Framework,
    Regex Pattern,
    IReadOnlyList<string> Extensions)
{
    public bool AppliesTo(string filePath) =>
        Extensions.Any(e => filePath.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
