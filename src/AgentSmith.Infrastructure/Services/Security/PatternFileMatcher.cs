using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Matches compiled regex patterns against individual file contents.
/// </summary>
internal static class PatternFileMatcher
{
    public static async Task<List<PatternFinding>> ScanFileAsync(
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

                        var provider = SecretProviderRegistry.Lookup(pattern.Definition.Id);

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
                            MatchedText: matchedText,
                            Provider: provider?.Name,
                            RevokeUrl: provider?.RevokeUrl));
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

    internal sealed record CompiledPattern(Regex Regex, PatternDefinition Definition);
}
