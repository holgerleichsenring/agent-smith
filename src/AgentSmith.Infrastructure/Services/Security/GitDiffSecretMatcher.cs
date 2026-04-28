using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Diff parsing, secret-pattern matching, and masking utilities used by
/// <see cref="GitHistoryScanner"/>. Patterns are passed in by the caller so this
/// stays a pure transformer over data — same pattern source as the static scanner.
/// </summary>
public sealed class GitDiffSecretMatcher
{
    public HashSet<string> BuildHeadContentIndex(
        Repository repo,
        IReadOnlyList<CompiledPattern> patterns)
    {
        var index = new HashSet<string>(StringComparer.Ordinal);

        if (repo.Head?.Tip?.Tree is null)
            return index;

        CollectBlobContent(repo.Head.Tip.Tree, patterns, index);
        return index;
    }

    public List<(int LineNumber, string Content)> ExtractAddedLines(string patch)
    {
        var result = new List<(int, string)>();
        if (string.IsNullOrEmpty(patch))
            return result;

        var lineNumber = 0;
        foreach (var line in patch.Split('\n'))
        {
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
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

    public string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return secret[..2] + new string('*', secret.Length - 2);

        return secret[..4] + new string('*', secret.Length - 6) + secret[^2..];
    }

    private static void CollectBlobContent(
        Tree tree,
        IReadOnlyList<CompiledPattern> patterns,
        HashSet<string> index)
    {
        foreach (var entry in tree)
        {
            if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                var blob = (Blob)entry.Target;
                using var reader = new StreamReader(blob.GetContentStream());
                var content = reader.ReadToEnd();

                foreach (var pattern in patterns)
                {
                    foreach (Match match in pattern.Regex.Matches(content))
                    {
                        index.Add(match.Value);
                    }
                }
            }
            else if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                CollectBlobContent((Tree)entry.Target, patterns, index);
            }
        }
    }
}
