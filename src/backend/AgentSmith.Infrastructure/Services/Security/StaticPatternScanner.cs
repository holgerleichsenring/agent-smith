using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Scans repository source files against compiled regex pattern definitions.
/// Routes filesystem reads through ISandboxFileReader so the scan covers the
/// sandbox /work tree regardless of backend.
/// </summary>
public sealed class StaticPatternScanner(
    PatternDefinitionLoader loader,
    PatternsDirectoryResolver directoryResolver,
    PatternCompiler compiler,
    PatternFileMatcher fileMatcher,
    ILogger<StaticPatternScanner> logger) : IStaticPatternScanner
{
    public async Task<StaticScanResult> ScanAsync(
        ISandboxFileReader reader, CancellationToken cancellationToken)
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
            Repository.SandboxWorkPath, compiledPatterns.Count);

        var findings = new List<PatternFinding>();
        var filesScanned = 0;

        await foreach (var filePath in SourceFileEnumerator.EnumerateAsync(
            reader, Repository.SandboxWorkPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = ToRelative(filePath, Repository.SandboxWorkPath);
            var fileFindings = await fileMatcher.ScanFileAsync(
                reader, filePath, relativePath, compiledPatterns, cancellationToken);
            findings.AddRange(fileFindings);
            filesScanned++;
        }

        sw.Stop();

        logger.LogInformation(
            "Static scan finished: {Findings} findings in {Files} files, {Duration}ms",
            findings.Count, filesScanned, sw.ElapsedMilliseconds);

        return new StaticScanResult(findings, filesScanned, compiledPatterns.Count, (int)sw.ElapsedMilliseconds);
    }

    private static string ToRelative(string fullPath, string root)
    {
        var rel = fullPath.Length > root.Length ? fullPath[root.Length..] : fullPath;
        return rel.TrimStart('/');
    }
}
