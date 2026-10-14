namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: reads one fact's evidence line into the paths it cites. Pure.
/// <para>
/// TOLERANT TOKENS, STRICT RESOLUTION. The record writes citations for a human — paths between
/// commas, in parentheses, with ', :47' continuing the previous file — and a strict grammar would
/// red a fifth of it. So punctuation is shed generously, but a token that looks like a path is
/// never skipped: a path whose lines do not parse comes back with them unparsed, for the check
/// to report.
/// </para>
/// </summary>
public sealed class EvidenceReferences
{
    private const string Observed = "observed:";

    public EvidenceReading Read(string evidence)
    {
        var observations = new List<string>();
        var minted = new List<string>();
        var cited = Walk(evidence, observations, minted);
        return new EvidenceReading([.. cited.Select(c => c.Reference)], observations, minted);
    }

    /// <summary>2026-10-13-8762n: every path token in reading order with the text it was read from — a
    /// ':'-continuation's text starts with ':' and its reference carries the path it binds to.</summary>
    public IReadOnlyList<EvidenceToken> Cited(string evidence) => Walk(evidence, [], []);

    private static List<EvidenceToken> Walk(string evidence, List<string> observations, List<string> minted)
    {
        var cited = new List<EvidenceToken>();
        EvidenceReference? previous = null;
        foreach (var raw in (evidence ?? string.Empty).Split(';'))
        {
            var segment = raw.Trim();
            if (segment.StartsWith(Observed, StringComparison.Ordinal)) observations.Add(segment);
            else if (EvidenceGrammar.Minted().IsMatch(segment)) minted.Add(segment);
            else
                foreach (var token in Tokens(segment))
                {
                    var reference = Reference(token, previous);
                    if (reference is null) continue;
                    cited.Add(new EvidenceToken(token, reference));
                    previous = reference;
                }
        }
        return cited;
    }

    private static IEnumerable<string> Tokens(string segment) =>
        segment.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.TrimStart('(', '`', '"').TrimEnd(',', ';', '.', ')', '`', '"'))
            .Where(t => t.Length > 0);

    private static EvidenceReference? Reference(string token, EvidenceReference? previous)
    {
        if (token.Contains("://", StringComparison.Ordinal)) return null;
        if (token[0] == ':')
            return previous is null ? null : WithLines(previous.Qualifier, previous.Path, token[1..]);

        var qualified = EvidenceGrammar.Qualified().Match(token);
        var qualifier = qualified.Success ? qualified.Groups["qualifier"].Value : null;
        var rest = qualified.Success ? qualified.Groups["rest"].Value : token;
        if (rest.Contains('/'))
        {
            var colon = rest.IndexOf(':');
            return colon < 0
                ? new EvidenceReference(qualifier, rest, null, null)
                : WithLines(qualifier, rest[..colon], rest[(colon + 1)..]);
        }

        var root = EvidenceGrammar.RootFileWithLines().Match(token);
        return root.Success ? WithLines(null, root.Groups["path"].Value, root.Groups["lines"].Value) : null;
    }

    private static EvidenceReference WithLines(string? qualifier, string path, string lines) =>
        new(qualifier, path, lines, EvidenceLines.Parse(lines));
}
