using System.Diagnostics;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// Drives the git CLI inside the sandbox (Step{Kind=Run}) and parses the unified
/// diff output via <see cref="GitDiffSecretMatcher"/>. No LibGit2Sharp dependency.
/// </summary>
public sealed class GitHistoryScanner(
    PatternDefinitionLoader loader,
    PatternsDirectoryResolver directoryResolver,
    PatternCompiler compiler,
    GitDiffSecretMatcher matcher,
    ILogger<GitHistoryScanner> logger) : IGitHistoryScanner
{
    private const int MaxCommits = 500;
    private const string SecretsCategory = "secrets";

    public async Task<GitHistoryScanResult> ScanAsync(
        ISandbox sandbox, ISandboxFileReader reader, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<HistoryFinding>();
        var commitsScanned = 0;

        var secretPatterns = LoadSecretPatterns();
        if (secretPatterns.Count == 0)
        {
            logger.LogWarning("No secret patterns loaded, git history scan returns empty result");
            return new GitHistoryScanResult([], 0, (int)sw.ElapsedMilliseconds);
        }

        var rawLog = await RunGitAsync(sandbox,
            ["log", $"--max-count={MaxCommits}", "-p", "--no-color", "--unified=0", "--format=__COMMIT__%n%H"],
            cancellationToken);
        if (rawLog is null)
        {
            logger.LogWarning("git log failed inside sandbox; returning empty git-history scan result");
            return new GitHistoryScanResult([], 0, (int)sw.ElapsedMilliseconds);
        }

        var headContent = await matcher.BuildHeadContentIndexAsync(
            reader, Repository.SandboxWorkPath, secretPatterns, cancellationToken);
        var seenSecrets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (sha, fileChanges) in ParseLog(rawLog))
        {
            cancellationToken.ThrowIfCancellationRequested();
            commitsScanned++;

            foreach (var (filePath, patch) in fileChanges)
            {
                ScanChange(filePath, patch, sha, secretPatterns, headContent, seenSecrets, findings);
            }
        }

        sw.Stop();

        logger.LogDebug(
            "Git history scan finished: {Findings} findings in {Commits} commits ({Duration}ms)",
            findings.Count, commitsScanned, sw.ElapsedMilliseconds);

        return new GitHistoryScanResult(findings, commitsScanned, (int)sw.ElapsedMilliseconds);
    }

    private IReadOnlyList<CompiledPattern> LoadSecretPatterns()
    {
        var dir = directoryResolver.Resolve();
        var definitions = loader.LoadFromDirectory(dir);
        var secretsOnly = definitions
            .Where(d => string.Equals(d.Category, SecretsCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return compiler.Compile(secretsOnly);
    }

    private static async Task<string?> RunGitAsync(
        ISandbox sandbox, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "git",
            Args: args,
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: 120);
        var progress = new Progress<StepEvent>(ev =>
        {
            if (ev.Kind == StepEventKind.Stdout) sb.AppendLine(ev.Line);
        });
        var result = await sandbox.RunStepAsync(step, progress, cancellationToken);
        return result.ExitCode == 0 ? sb.ToString() : null;
    }

    private static IEnumerable<(string Sha, IReadOnlyList<(string File, string Patch)> Changes)> ParseLog(string rawLog)
    {
        // Splits on the __COMMIT__ marker; each chunk starts with the SHA on the next line,
        // followed by zero or more `diff --git ...` blocks for that commit.
        var chunks = rawLog.Split(new[] { "__COMMIT__\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var chunk in chunks)
        {
            var newlineIdx = chunk.IndexOf('\n');
            if (newlineIdx < 0) continue;
            var sha = chunk[..newlineIdx].Trim();
            if (sha.Length == 0) continue;

            var diffs = SplitDiffs(chunk[(newlineIdx + 1)..]);
            yield return (sha, diffs);
        }
    }

    private static List<(string File, string Patch)> SplitDiffs(string body)
    {
        var diffs = new List<(string, string)>();
        var splits = body.Split(new[] { "diff --git " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in splits)
        {
            var newlineIdx = part.IndexOf('\n');
            if (newlineIdx < 0) continue;
            var header = part[..newlineIdx];
            var path = ExtractPathFromDiffHeader(header);
            if (path is null) continue;
            diffs.Add((path, part[(newlineIdx + 1)..]));
        }
        return diffs;
    }

    private static string? ExtractPathFromDiffHeader(string header)
    {
        // Header format: "a/path/to/file b/path/to/file"
        var space = header.IndexOf(" b/", StringComparison.Ordinal);
        return space > 2 ? header[2..space] : null;
    }

    private void ScanChange(
        string file, string patch, string commitSha,
        IReadOnlyList<CompiledPattern> secretPatterns,
        HashSet<string> headContent, HashSet<string> seenSecrets,
        List<HistoryFinding> findings)
    {
        var addedLines = matcher.ExtractAddedLines(patch);

        foreach (var (lineNumber, line) in addedLines)
        {
            foreach (var pattern in secretPatterns)
            {
                var match = pattern.Regex.Match(line);
                if (!match.Success) continue;

                var matchedText = match.Value;
                if (!seenSecrets.Add(matchedText)) continue;

                var stillInTree = headContent.Contains(matchedText);
                var severity = stillInTree ? "HIGH" : "CRITICAL";

                findings.Add(new HistoryFinding(
                    PatternId: pattern.Definition.Id,
                    Severity: severity,
                    CommitHash: commitSha,
                    File: file,
                    Line: lineNumber,
                    Title: pattern.Definition.Title,
                    Description: stillInTree
                        ? "Secret found in commit history and still present in working tree"
                        : "Secret found in commit history but removed from working tree",
                    MatchedText: matcher.MaskSecret(matchedText),
                    StillInWorkingTree: stillInTree,
                    Provider: pattern.Definition.Provider,
                    RevokeUrl: pattern.Definition.RevocationUrl));
            }
        }
    }
}
