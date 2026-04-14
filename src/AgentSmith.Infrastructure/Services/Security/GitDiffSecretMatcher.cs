using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Diff parsing, secret-pattern matching, and masking utilities
/// extracted from <see cref="GitHistoryScanner"/>.
/// </summary>
internal static class GitDiffSecretMatcher
{
    internal static readonly (string Id, string Title, Regex Pattern)[] SecretPatterns =
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

    /// <summary>
    /// Builds a searchable index of all secret-matching text found in HEAD blobs.
    /// </summary>
    internal static HashSet<string> BuildHeadContentIndex(Repository repo)
    {
        var index = new HashSet<string>(StringComparer.Ordinal);

        if (repo.Head?.Tip?.Tree is null)
            return index;

        CollectBlobContent(repo.Head.Tip.Tree, index);
        return index;
    }

    /// <summary>
    /// Extracts added lines (lines starting with '+') from a unified diff patch,
    /// returning tuples of (approximate line number, line content).
    /// </summary>
    internal static List<(int LineNumber, string Content)> ExtractAddedLines(string patch)
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
    internal static string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return secret[..2] + new string('*', secret.Length - 2);

        return secret[..4] + new string('*', secret.Length - 6) + secret[^2..];
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
}
