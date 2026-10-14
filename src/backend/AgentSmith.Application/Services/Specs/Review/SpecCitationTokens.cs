using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: the citations a re-ground can address — unqualified path:lines tokens in the facts'
/// evidence, each with every fact that carries it. Minted look lines, observations, path-only tokens,
/// ':'-continuations and qualified tokens name no line text or no repository, and are left alone.
/// </summary>
internal static class SpecCitationTokens
{
    public static IReadOnlyList<CitedToken> Of(string yaml, EvidenceReferences references)
    {
        var cited = new Dictionary<string, CitedToken>(StringComparer.Ordinal);
        foreach (var (index, evidence) in SpecFactRegrounding.Evidence(yaml))
        {
            var tokens = references.Cited(evidence);
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.IsContinuation || token.Reference is not { Qualifier: null, Lines.Count: > 0 }) continue;
                var continued = i + 1 < tokens.Count && tokens[i + 1].IsContinuation;
                cited[token.Text] = cited.TryGetValue(token.Text, out var seen)
                    ? seen with { Continued = seen.Continued || continued, Facts = [.. seen.Facts, index] }
                    : new CitedToken(token.Text, token.Reference, continued, [index]);
            }
        }
        return [.. cited.Values];
    }

    /// <summary>Ranges as a citation writes them: '12' for one line, '12-20' for several, comma-joined.</summary>
    public static string Format(IEnumerable<EvidenceLineRange> ranges) =>
        string.Join(",", ranges.Select(r => r.From == r.To ? $"{r.From}" : $"{r.From}-{r.To}"));
}

/// <summary>2026-10-13-8762n: one addressable citation, whether a ':'-continuation binds to it, and the facts carrying it.</summary>
internal sealed record CitedToken(string Text, EvidenceReference Reference, bool Continued, IReadOnlyList<int> Facts)
{
    public IReadOnlyList<EvidenceLineRange> Ranges => Reference.Lines!;
}

/// <summary>2026-10-13-8762n: where a citation was routed — its repository, the path as git there reads it,
/// the repository-name prefix the token carried (or none), and the tree it was written against.</summary>
internal sealed record CitationRoute(string Repository, ISandbox Sandbox, string Path, string Prefix, string Base);

/// <summary>2026-10-13-8762n: one citation that moved, the token it becomes and the facts it is spliced into.</summary>
public sealed record SpecCitationMove(string Old, string New, IReadOnlyList<int> Facts);

/// <summary>2026-10-13-8762n: the entered spec after re-grounding — its yaml with every spliced move, the
/// moves spliced, the stale citations (one line each), and the citations past the read budget.</summary>
public sealed record SpecReground(string Yaml, IReadOnlyList<SpecCitationMove> Moves, IReadOnlyList<string> Stale, int Unchecked)
{
    public static SpecReground Unchanged(string yaml) => new(yaml, [], [], 0);
}
