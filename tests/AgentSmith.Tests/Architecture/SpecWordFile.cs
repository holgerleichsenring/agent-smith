using System.Text.RegularExpressions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-12-967ag: one file as the spec-word guard reads it — every token that contains
/// the word, counted, from the file's path and from its text alike. A token is a run of
/// letters, digits, underscores and hyphens, so <c>PhaseId</c>, <c>phase_review</c>,
/// <c>phase-gate</c> and a test name are each one token, and the bare word is its own.
/// Comments, literals, identifiers and prose are read the same way; what tells a kept
/// meaning from the old noun is the allowlist, not where the token stands.
/// </summary>
internal sealed partial record SpecWordFile(string Path, IReadOnlyDictionary<string, int> Tokens)
{
    [GeneratedRegex("[A-Za-z0-9_-]*[Pp][Hh][Aa][Ss][Ee][A-Za-z0-9_-]*")]
    private static partial Regex Word();

    public static SpecWordFile Of(string path, string text) =>
        new(path, Word().Matches(path).Concat(Word().Matches(text))
            .Select(match => match.Value.Trim('-'))
            .GroupBy(token => token, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
}
