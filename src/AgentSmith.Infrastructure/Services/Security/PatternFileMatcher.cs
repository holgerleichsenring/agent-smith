using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Matches compiled regex patterns against individual file contents.
/// </summary>
public sealed class PatternFileMatcher
{
    public async Task<List<PatternFinding>> ScanFileAsync(
        string filePath,
        string relativePath,
        IReadOnlyList<CompiledPattern> patterns,
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
                            Provider: pattern.Definition.Provider,
                            RevokeUrl: pattern.Definition.RevocationUrl));
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                }
            }
        }

        return findings;
    }
}
