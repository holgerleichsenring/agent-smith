using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Architecture;

namespace AgentSmith.Tests.Verification;

/// <summary>
/// 2026-08-30-0ea8: locates the checked-in verification standard and its lens table, so
/// the rules that judge them read the FILES the build embeds rather than a copy of what
/// somebody remembered they said.
/// </summary>
internal static class CheckedInVerificationFiles
{
    private static readonly string ProjectDir =
        Path.Combine(ArchitectureSources.BackendRoot, "AgentSmith.Infrastructure.Core");

    public static string ExportPath { get; } =
        Path.Combine(ProjectDir, "Resources", "asvs-requirements.flat.json");

    public static string LensPath { get; } = Path.Combine(ProjectDir, "Resources", "asvs-lens.tsv");

    public static string ProjectPath { get; } =
        Path.Combine(ProjectDir, "AgentSmith.Infrastructure.Core.csproj");

    /// <summary>The export read independently of the product's own parser.</summary>
    public static IReadOnlyList<VerificationRequirement> ExportedRequirements()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ExportPath));
        return
        [
            .. document.RootElement.GetProperty("requirements").EnumerateArray()
                .Select(entry => new VerificationRequirement(
                    entry.GetProperty("req_id").GetString()!,
                    entry.GetProperty("L").GetString()!,
                    entry.GetProperty("req_description").GetString()!))
        ];
    }

    /// <summary>The value of one MSBuild property of the embedding project.</summary>
    public static string ProjectProperty(string name)
    {
        var project = File.ReadAllText(ProjectPath);
        var open = project.IndexOf($"<{name}>", StringComparison.Ordinal) + name.Length + 2;
        var close = project.IndexOf($"</{name}>", StringComparison.Ordinal);
        return open <= name.Length + 1 || close < open
            ? throw new InvalidOperationException($"The project declares no <{name}> property.")
            : project[open..close].Trim();
    }
}
