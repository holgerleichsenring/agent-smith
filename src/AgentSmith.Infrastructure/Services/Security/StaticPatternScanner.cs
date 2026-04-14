using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Scans repository source files against compiled regex pattern definitions.
/// Respects .gitignore-like directory exclusions and binary file extensions.
/// </summary>
public sealed class StaticPatternScanner(
    PatternDefinitionLoader loader,
    ILogger<StaticPatternScanner> logger) : IStaticPatternScanner
{
    /// <summary>
    /// Resolves the patterns directory from standard config locations.
    /// </summary>
    private static string ResolvePatternsDirectory()
    {
        var candidates = new[]
        {
            Path.Combine("config", "patterns"),
            Path.Combine(AppContext.BaseDirectory, "config", "patterns"),
        };

        var configDir = Environment.GetEnvironmentVariable("AGENTSMITH_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            return Directory.Exists(Path.Combine(configDir, "patterns"))
                ? Path.Combine(configDir, "patterns")
                : Directory.Exists(Path.Combine(configDir, "config", "patterns"))
                    ? Path.Combine(configDir, "config", "patterns")
                    : candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    public async Task<StaticScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var patternsDirectory = ResolvePatternsDirectory();
        var definitions = loader.LoadFromDirectory(patternsDirectory);
        if (definitions.Count == 0)
        {
            logger.LogWarning("No pattern definitions loaded, returning empty result");
            return new StaticScanResult([], 0, 0, (int)sw.ElapsedMilliseconds);
        }

        var compiledPatterns = CompilePatterns(definitions);

        logger.LogInformation(
            "Scanning {RepoPath} with {PatternCount} compiled patterns",
            repoPath, compiledPatterns.Count);

        var findings = new List<PatternFinding>();
        var filesScanned = 0;

        var files = SourceFileEnumerator.EnumerateSourceFiles(repoPath);

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(repoPath, filePath);
            var fileFindings = await PatternFileMatcher.ScanFileAsync(
                filePath, relativePath, compiledPatterns, cancellationToken);
            findings.AddRange(fileFindings);
            filesScanned++;
        }

        sw.Stop();

        logger.LogInformation(
            "Static scan finished: {Findings} findings in {Files} files, {Duration}ms",
            findings.Count, filesScanned, sw.ElapsedMilliseconds);

        return new StaticScanResult(findings, filesScanned, compiledPatterns.Count, (int)sw.ElapsedMilliseconds);
    }

    private List<PatternFileMatcher.CompiledPattern> CompilePatterns(IReadOnlyList<PatternDefinition> definitions)
    {
        var compiled = new List<PatternFileMatcher.CompiledPattern>();

        foreach (var def in definitions)
        {
            if (string.IsNullOrWhiteSpace(def.Regex))
                continue;

            try
            {
                var regex = new Regex(
                    def.Regex,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(500));

                compiled.Add(new PatternFileMatcher.CompiledPattern(regex, def));
            }
            catch (RegexParseException ex)
            {
                logger.LogWarning(ex, "Invalid regex in pattern {PatternId}: {Regex}", def.Id, def.Regex);
            }
        }

        return compiled;
    }
}
