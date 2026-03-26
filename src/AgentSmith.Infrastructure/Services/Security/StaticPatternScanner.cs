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
    private const long MaxFileSizeBytes = 1_048_576; // 1 MB

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", "dist", "build",
        "vendor", "__pycache__", ".vs", ".idea", "packages"
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".woff", ".woff2", ".ttf", ".eot",
        ".svg", ".zip", ".tar", ".gz", ".exe", ".dll", ".so", ".dylib",
        ".pdf", ".mp3", ".mp4", ".lock", ".min.js", ".min.css"
    };

    private string _patternsDirectory = Path.Combine(AppContext.BaseDirectory, "config", "patterns");

    /// <summary>
    /// Sets the directory where pattern YAML files are located.
    /// Defaults to config/patterns relative to the application base directory.
    /// </summary>
    public string PatternsDirectory
    {
        get => _patternsDirectory;
        set => _patternsDirectory = value;
    }

    public async Task<StaticScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var definitions = loader.LoadFromDirectory(_patternsDirectory);
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

        var files = EnumerateSourceFiles(repoPath);

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(repoPath, filePath);
            var fileFindings = await ScanFileAsync(filePath, relativePath, compiledPatterns, cancellationToken);
            findings.AddRange(fileFindings);
            filesScanned++;
        }

        sw.Stop();

        logger.LogInformation(
            "Static scan finished: {Findings} findings in {Files} files, {Duration}ms",
            findings.Count, filesScanned, sw.ElapsedMilliseconds);

        return new StaticScanResult(findings, filesScanned, compiledPatterns.Count, (int)sw.ElapsedMilliseconds);
    }

    private List<CompiledPattern> CompilePatterns(IReadOnlyList<PatternDefinition> definitions)
    {
        var compiled = new List<CompiledPattern>();

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

                compiled.Add(new CompiledPattern(regex, def));
            }
            catch (RegexParseException ex)
            {
                logger.LogWarning(ex, "Invalid regex in pattern {PatternId}: {Regex}", def.Id, def.Regex);
            }
        }

        return compiled;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string repoPath)
    {
        var stack = new Stack<string>();
        stack.Push(repoPath);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                if (IsBinaryFile(fileName))
                    continue;

                try
                {
                    var info = new FileInfo(file);
                    if (info.Length > MaxFileSizeBytes)
                        continue;
                }
                catch
                {
                    continue;
                }

                yield return file;
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                if (!ExcludedDirectories.Contains(dirName))
                    stack.Push(subdir);
            }
        }
    }

    private static bool IsBinaryFile(string fileName)
    {
        // Check compound extensions like .min.js, .min.css
        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
    }

    private static async Task<List<PatternFinding>> ScanFileAsync(
        string filePath,
        string relativePath,
        List<CompiledPattern> patterns,
        CancellationToken cancellationToken)
    {
        var findings = new List<PatternFinding>();

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        }
        catch
        {
            return findings;
        }

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (var pattern in patterns)
            {
                try
                {
                    var match = pattern.Regex.Match(line);
                    if (match.Success)
                    {
                        var matchedText = match.Value.Length > 200
                            ? match.Value[..200] + "..."
                            : match.Value;

                        findings.Add(new PatternFinding(
                            PatternId: pattern.Definition.Id,
                            Category: pattern.Definition.Category,
                            Severity: pattern.Definition.Severity,
                            Confidence: pattern.Definition.Confidence,
                            File: relativePath,
                            Line: lineIndex + 1,
                            Title: pattern.Definition.Title,
                            Description: pattern.Definition.Description,
                            Cwe: pattern.Definition.Cwe,
                            MatchedText: matchedText));
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Pattern took too long on this line, skip it
                }
            }
        }

        return findings;
    }

    private sealed record CompiledPattern(Regex Regex, PatternDefinition Definition);
}
