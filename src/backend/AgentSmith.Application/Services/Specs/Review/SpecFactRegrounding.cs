using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: rewrites one citation token inside ONE fact's evidence scalar and nowhere else. The
/// scalar is located by its source span in YamlDotNet's representation model, so every other byte of the
/// file stays as written; a token that is not verbatim in that span (an escaped scalar) is not rewritten.
/// </summary>
public static class SpecFactRegrounding
{
    /// <summary>Each fact's position in the <c>facts</c> sequence and its evidence text, for facts that have one.</summary>
    public static IReadOnlyList<(int Index, string Evidence)> Evidence(string yaml) =>
        [.. Facts(yaml).Select((fact, i) => (i, Scalar(fact)?.Value)).Where(f => f.Value is not null).Select(f => (f.i, f.Value!))];

    /// <summary>The yaml with every whole-token occurrence of <paramref name="oldToken"/> in fact
    /// <paramref name="factIndex"/>'s evidence replaced; null when the token is not verbatim in that scalar.</summary>
    public static string? Apply(string yaml, int factIndex, string oldToken, string newToken)
    {
        if (Facts(yaml).ElementAtOrDefault(factIndex) is not { } fact || Scalar(fact) is not { } scalar) return null;
        var (start, end) = ((int)scalar.Start.Index, (int)scalar.End.Index);
        var span = yaml[start..end];
        var count = WholeTokens(span, oldToken).Count;
        if (count == 0 || count != WholeTokens(scalar.Value ?? string.Empty, oldToken).Count) return null;
        var rewritten = span;
        foreach (var at in WholeTokens(span, oldToken).OrderDescending())
            rewritten = rewritten[..at] + newToken + rewritten[(at + oldToken.Length)..];
        return yaml[..start] + rewritten + yaml[end..];
    }

    /// <summary>The token replaced in every listed fact, or null — and the yaml untouched — when any one cannot be.</summary>
    public static string? ApplyAll(string yaml, IEnumerable<int> factIndexes, string oldToken, string newToken)
    {
        string? current = yaml;
        foreach (var index in factIndexes)
            if ((current = Apply(current, index, oldToken, newToken)) is null) return null;
        return current;
    }

    private static IReadOnlyList<int> WholeTokens(string text, string token)
    {
        var found = new List<int>();
        for (var at = text.IndexOf(token, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(token, at + 1, StringComparison.Ordinal))
            if (Bounded(text, at, token.Length)) found.Add(at);
        return found;
    }

    // A path character on either side, or ',' '.' followed by more of a token, continues a longer token.
    private static bool Bounded(string text, int at, int length)
    {
        if (at > 0 && IsTokenChar(text[at - 1])) return false;
        var after = at + length;
        if (after >= text.Length) return true;
        if (IsTokenChar(text[after]) && text[after] != '.') return false;
        return text[after] is not ('.' or ',') || after + 1 >= text.Length || !char.IsLetterOrDigit(text[after + 1]);
    }

    private static bool IsTokenChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '/' or ':' or '-' or '.';

    private static IEnumerable<YamlMappingNode> Facts(string yaml)
    {
        var stream = new YamlStream();
        using (var reader = new StringReader(yaml)) stream.Load(reader);
        return stream.Documents.FirstOrDefault()?.RootNode is YamlMappingNode root
            && root.Children.TryGetValue(new YamlScalarNode("facts"), out var facts) && facts is YamlSequenceNode list
            ? list.Children.Select(f => f as YamlMappingNode ?? new YamlMappingNode()) : [];
    }

    private static YamlScalarNode? Scalar(YamlMappingNode fact) =>
        fact.Children.TryGetValue(new YamlScalarNode("evidence"), out var node) ? node as YamlScalarNode : null;
}
