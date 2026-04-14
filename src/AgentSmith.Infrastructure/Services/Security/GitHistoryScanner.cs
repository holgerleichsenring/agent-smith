using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// Delegates diff parsing, pattern matching and masking to <see cref="GitDiffSecretMatcher"/>.
/// </summary>
public sealed class GitHistoryScanner(ILogger<GitHistoryScanner> logger) : IGitHistoryScanner
{
    private const int MaxCommits = 500;

    public Task<GitHistoryScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<HistoryFinding>();
        var commitsScanned = 0;

        try
        {
            using var repo = new Repository(repoPath);
            var headContent = GitDiffSecretMatcher.BuildHeadContentIndex(repo);
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

                    ScanChange(change, commit.Sha, headContent, seenSecrets, findings);
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

    private static void ScanChange(
        PatchEntryChanges change, string commitSha,
        HashSet<string> headContent, HashSet<string> seenSecrets,
        List<HistoryFinding> findings)
    {
        var addedLines = GitDiffSecretMatcher.ExtractAddedLines(change.Patch);

        foreach (var (lineNumber, line) in addedLines)
        {
            foreach (var (id, title, pattern) in GitDiffSecretMatcher.SecretPatterns)
            {
                var match = pattern.Match(line);
                if (!match.Success) continue;

                var matchedText = match.Value;
                if (!seenSecrets.Add(matchedText)) continue;

                var stillInTree = headContent.Contains(matchedText);
                var severity = stillInTree ? "HIGH" : "CRITICAL";
                var provider = SecretProviderRegistry.Lookup(id);

                findings.Add(new HistoryFinding(
                    PatternId: id,
                    Severity: severity,
                    CommitHash: commitSha,
                    File: change.Path,
                    Line: lineNumber,
                    Title: title,
                    Description: stillInTree
                        ? "Secret found in commit history and still present in working tree"
                        : "Secret found in commit history but removed from working tree",
                    MatchedText: GitDiffSecretMatcher.MaskSecret(matchedText),
                    StillInWorkingTree: stillInTree,
                    Provider: provider?.Name,
                    RevokeUrl: provider?.RevokeUrl));
            }
        }
    }
}
