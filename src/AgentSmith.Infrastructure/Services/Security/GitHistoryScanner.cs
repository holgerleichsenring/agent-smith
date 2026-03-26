using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// Uses LibGit2Sharp to walk commits and diff against parents.
/// </summary>
public sealed class GitHistoryScanner(ILogger<GitHistoryScanner> logger) : IGitHistoryScanner
{
    private const int MaxCommits = 500;

    private static readonly (string Id, string Title, Regex Pattern)[] SecretPatterns =
    [
        ("aws-key", "AWS Access Key", new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)),
        ("github-token", "GitHub Token", new Regex(@"gh[pors]_[A-Za-z0-9]{36}", RegexOptions.Compiled)),
        ("generic-api-key", "Generic API Key", new Regex(@"(api[_\-]?key|apikey|api[_\-]?secret)\s*[:=]\s*[""'][A-Za-z0-9/+=]{20,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("private-key", "Private Key", new Regex(@"-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", RegexOptions.Compiled)),
        ("connection-string", "Connection String", new Regex(@"(mongodb(\+srv)?|postgres(ql)?|mysql|redis|amqp)://[^\s""']+:[^\s""']+@", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("generic-secret", "Generic Secret", new Regex(@"(secret|password|passwd|token|credential)\s*[:=]\s*[""'][^\s""']{8,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("stripe-key", "Stripe Secret Key", new Regex(@"sk_live_[A-Za-z0-9]{24,}", RegexOptions.Compiled)),
        ("slack-token", "Slack Token", new Regex(@"xox[bpors]-[A-Za-z0-9\-]{10,}", RegexOptions.Compiled)),
    ];

    public Task<GitHistoryScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<HistoryFinding>();
        var commitsScanned = 0;

        try
        {
            using var repo = new Repository(repoPath);

            // Build a set of all content in HEAD for "still in working tree" checks
            var headContent = BuildHeadContentIndex(repo);

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

                    var addedLines = ExtractAddedLines(change.Patch);

                    foreach (var (lineNumber, line) in addedLines)
                    {
                        foreach (var (id, title, pattern) in SecretPatterns)
                        {
                            var match = pattern.Match(line);
                            if (!match.Success)
                                continue;

                            var matchedText = match.Value;

                            // Deduplicate by matched text — keep first occurrence only
                            if (!seenSecrets.Add(matchedText))
                                continue;

                            var stillInTree = headContent.Contains(matchedText);
                            var severity = stillInTree ? "HIGH" : "CRITICAL";

                            findings.Add(new HistoryFinding(
                                PatternId: id,
                                Severity: severity,
                                CommitHash: commit.Sha,
                                File: change.Path,
                                Line: lineNumber,
                                Title: title,
                                Description: stillInTree
                                    ? $"Secret found in commit history and still present in working tree"
                                    : $"Secret found in commit history but removed from working tree",
                                MatchedText: MaskSecret(matchedText),
                                StillInWorkingTree: stillInTree));
                        }
                    }
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
            findings,
            commitsScanned,
            (int)sw.ElapsedMilliseconds));
    }

    /// <summary>
    /// Builds a searchable index of all blob content in HEAD for working-tree presence checks.
    /// </summary>
    private static HashSet<string> BuildHeadContentIndex(Repository repo)
    {
        var index = new HashSet<string>(StringComparer.Ordinal);

        if (repo.Head?.Tip?.Tree is null)
            return index;

        CollectBlobContent(repo.Head.Tip.Tree, index);
        return index;
    }

    private static void CollectBlobContent(Tree tree, HashSet<string> index)
    {
        foreach (var entry in tree)
        {
            if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)entry.Target;
                using var reader = new StreamReader(blob.GetContentStream());
                var content = reader.ReadToEnd();

                // Check each secret pattern against the full blob content
                foreach (var (_, _, pattern) in SecretPatterns)
                {
                    foreach (Match match in pattern.Matches(content))
                    {
                        index.Add(match.Value);
                    }
                }
            }
            else if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                CollectBlobContent((Tree)entry.Target, index);
            }
        }
    }

    /// <summary>
    /// Extracts added lines (lines starting with '+') from a unified diff patch,
    /// returning tuples of (approximate line number, line content).
    /// </summary>
    private static List<(int LineNumber, string Content)> ExtractAddedLines(string patch)
    {
        var result = new List<(int, string)>();
        if (string.IsNullOrEmpty(patch))
            return result;

        var lineNumber = 0;
        foreach (var line in patch.Split('\n'))
        {
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                // Parse new-file line number from hunk header: @@ -a,b +c,d @@
                var plusIndex = line.IndexOf('+');
                var commaIndex = line.IndexOf(',', plusIndex);
                var endIndex = commaIndex > 0 ? commaIndex : line.IndexOf(' ', plusIndex);
                if (plusIndex >= 0 && endIndex > plusIndex &&
                    int.TryParse(line.AsSpan(plusIndex + 1, endIndex - plusIndex - 1), out var parsed))
                {
                    lineNumber = parsed;
                }

                continue;
            }

            if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
            {
                result.Add((lineNumber, line[1..]));
                lineNumber++;
            }
            else if (!line.StartsWith('-'))
            {
                lineNumber++;
            }
        }

        return result;
    }

    /// <summary>
    /// Masks a secret value, keeping only the first 4 and last 2 characters visible.
    /// </summary>
    private static string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return secret[..2] + new string('*', secret.Length - 2);

        return secret[..4] + new string('*', secret.Length - 6) + secret[^2..];
    }
}
