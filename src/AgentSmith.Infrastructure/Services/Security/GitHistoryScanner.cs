using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// Loads the same secret patterns as the static scanner (category=secrets) and
/// delegates diff parsing, pattern matching and masking to <see cref="GitDiffSecretMatcher"/>.
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

    public Task<GitHistoryScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<HistoryFinding>();
        var commitsScanned = 0;

        var secretPatterns = LoadSecretPatterns();
        if (secretPatterns.Count == 0)
        {
            logger.LogWarning("No secret patterns loaded, git history scan returns empty result");
            return Task.FromResult(new GitHistoryScanResult([], 0, (int)sw.ElapsedMilliseconds));
        }

        try
        {
            using var repo = new Repository(repoPath);
            var headContent = matcher.BuildHeadContentIndex(repo, secretPatterns);
            var seenSecrets = new HashSet<string>(StringComparer.Ordinal);

            foreach (var commit in repo.Commits.Take(MaxCommits))
            {
                cancellationToken.ThrowIfCancellationRequested();
                commitsScanned++;

                var parent = commit.Parents.FirstOrDefault();
                var diff = repo.Diff.Compare<Patch>(parent?.Tree, commit.Tree);

                foreach (var change in diff)
                {
                    if (change.Status == LibGit2Sharp.ChangeKind.Deleted)
                        continue;

                    ScanChange(change, commit.Sha, secretPatterns, headContent, seenSecrets, findings);
                }
            }
        }
        catch (RepositoryNotFoundException ex)
        {
            logger.LogWarning(ex, "Repository not found at {Path}, returning empty scan result", repoPath);
        }
        catch (LibGit2SharpException ex)
        {
            logger.LogWarning(ex, "LibGit2Sharp error scanning {Path}, returning empty scan result", repoPath);
        }

        sw.Stop();

        logger.LogDebug(
            "Git history scan finished: {Findings} findings in {Commits} commits ({Duration}ms)",
            findings.Count, commitsScanned, sw.ElapsedMilliseconds);

        return Task.FromResult(new GitHistoryScanResult(
            findings, commitsScanned, (int)sw.ElapsedMilliseconds));
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

    private void ScanChange(
        PatchEntryChanges change, string commitSha,
        IReadOnlyList<CompiledPattern> secretPatterns,
        HashSet<string> headContent, HashSet<string> seenSecrets,
        List<HistoryFinding> findings)
    {
        var addedLines = matcher.ExtractAddedLines(change.Patch);

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
                    File: change.Path,
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
