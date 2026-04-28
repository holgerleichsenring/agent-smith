using System.Diagnostics;
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
    PatternsDirectoryResolver directoryResolver,
    PatternCompiler compiler,
    PatternFileMatcher fileMatcher,
    ILogger<StaticPatternScanner> logger) : IStaticPatternScanner
{
    public async Task<StaticScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var patternsDirectory = directoryResolver.Resolve();
        var definitions = loader.LoadFromDirectory(patternsDirectory);
        if (definitions.Count == 0)
        {
            logger.LogWarning("No pattern definitions loaded, returning empty result");
            return new StaticScanResult([], 0, 0, (int)sw.ElapsedMilliseconds);
        }

        var compiledPatterns = compiler.Compile(definitions);

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
            var fileFindings = await fileMatcher.ScanFileAsync(
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
}
