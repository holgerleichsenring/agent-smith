using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Matches compiled regex patterns against individual file contents fetched
/// through ISandboxFileReader so the scan reads the sandbox tree directly.
/// </summary>
public sealed class PatternFileMatcher
{
    public async Task<List<PatternFinding>> ScanFileAsync(
        ISandboxFileReader reader,
        string filePath,
        string relativePath,
        IReadOnlyList<CompiledPattern> patterns,
        CancellationToken cancellationToken)
    {
        var findings = new List<PatternFinding>();

        var content = await reader.TryReadAsync(filePath, cancellationToken);
        if (content is null) return findings;
        var lines = content.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].TrimEnd('\r');
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
