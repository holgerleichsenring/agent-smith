using System.Text.RegularExpressions;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Diff parsing, secret-pattern matching, and masking utilities used by
/// <see cref="GitHistoryScanner"/>. Patterns are passed in by the caller so this
/// stays a pure transformer over data — same pattern source as the static scanner.
/// HEAD content is fetched through ISandboxFileReader (no LibGit2Sharp).
/// </summary>
public sealed class GitDiffSecretMatcher
{
    public async Task<HashSet<string>> BuildHeadContentIndexAsync(
        ISandboxFileReader reader,
        string repoPath,
        IReadOnlyList<CompiledPattern> patterns,
        CancellationToken cancellationToken)
    {
        var index = new HashSet<string>(StringComparer.Ordinal);
        var entries = await reader.ListAsync(repoPath, maxDepth: 12, cancellationToken);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await reader.TryReadAsync(entry, cancellationToken);
            if (content is null) continue;

            foreach (var pattern in patterns)
                foreach (Match match in pattern.Regex.Matches(content))
                    index.Add(match.Value);
        }

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
}
