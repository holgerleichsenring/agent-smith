using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Analyzes the repository structure and dependencies.
/// </summary>
public sealed class AnalyzeCodeHandler(
    ILogger<AnalyzeCodeHandler> logger)
    : ICommandHandler<AnalyzeCodeContext>
{
    public Task<CommandResult> ExecuteAsync(
        AnalyzeCodeContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Analyzing code in {Path}...", context.Repository.LocalPath);

        var repoPath = context.Repository.LocalPath;
        var fileStructure = ScanFileStructure(repoPath);
        var dependencies = DetectDependencies(repoPath);
        var (framework, language) = DetectProjectType(repoPath);

        var analysis = new CodeAnalysis(fileStructure, dependencies, framework, language);

        context.Pipeline.Set(ContextKeys.CodeAnalysis, analysis);
        return Task.FromResult(
            CommandResult.Ok($"Code analysis completed: {fileStructure.Count} files found"));
    }

    private static IReadOnlyList<string> ScanFileStructure(string repoPath)
    {
        if (!Directory.Exists(repoPath))
            return Array.Empty<string>();

        return Directory.GetFiles(repoPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(repoPath, f))
            .Where(f => !f.StartsWith(".git" + Path.DirectorySeparatorChar))
            .OrderBy(f => f)
            .ToList();
    }

    private static IReadOnlyList<string> DetectDependencies(string repoPath)
    {
        var deps = new List<string>();

        // .NET: parse csproj PackageReference
        foreach (var csproj in Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories))
        {
            deps.AddRange(ExtractPackageReferences(csproj));
        }

        // Node.js: read package.json dependencies
        var packageJson = Path.Combine(repoPath, "package.json");
        if (File.Exists(packageJson))
            deps.Add("[package.json detected]");

        return deps.Distinct().OrderBy(d => d).ToList();
    }

    private static IEnumerable<string> ExtractPackageReferences(string csprojPath)
    {
        var content = File.ReadAllText(csprojPath);
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content, """PackageReference Include="([^"]+)"[^/]*Version="([^"]+)""");

        return matches.Select(m => $"{m.Groups[1].Value} ({m.Groups[2].Value})");
    }

    private static (string? framework, string? language) DetectProjectType(string repoPath)
    {
        if (Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories).Length > 0)
            return ("dotnet", "C#");

        if (File.Exists(Path.Combine(repoPath, "package.json")))
            return ("node", "TypeScript/JavaScript");

        if (Directory.GetFiles(repoPath, "*.py", SearchOption.AllDirectories).Length > 0)
            return ("python", "Python");

        return (null, null);
    }
}
