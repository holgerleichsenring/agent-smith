using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// 2026-11-23-933dc: runs every pattern on one line and returns EVERY match, in pattern order, each with its index and
/// length (<c>firstOnly</c>: each pattern's first match only, the security scan's reading). A pattern whose match times out keeps the matches it found before the timeout and is reported to
/// <c>onTimeout</c>; the caller decides whether a timeout is swallowed (the security scan) or fails (the PoC scan).
/// </summary>
public sealed class PatternLineMatcher(ILogger<PatternLineMatcher>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<PatternLineMatcher>.Instance;

    public IReadOnlyList<PatternLineMatch> Match(
        string line, IReadOnlyList<CompiledPattern> patterns, Action<CompiledPattern>? onTimeout = null, bool firstOnly = false)
    {
        var matches = new List<PatternLineMatch>();
        foreach (var pattern in patterns)
        {
            try
            {
                for (var match = pattern.Regex.Match(line); match.Success; match = match.NextMatch())
                {
                    matches.Add(new PatternLineMatch(pattern, match.Index, match.Length, match.Value));
                    if (firstOnly || match.Length == 0) break;
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogDebug(ex, "Pattern {PatternId} timed out on a line", pattern.Definition.Id);
                onTimeout?.Invoke(pattern);
            }
        }
        return matches;
    }
}
