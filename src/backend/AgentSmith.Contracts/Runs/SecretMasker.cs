using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423: replaces the credential values this framework itself staged before anything is
/// written to a trace.
/// <para>
/// Masking is a REPLACEMENT of known strings, never a guess at what looks secret. The
/// values are known — the framework put them into the sandbox — so there is no pattern to
/// match and no false sense of safety from one. A trace nobody may share is a trace nobody
/// will use, and a trace that leaks a token once is worse than none.
/// </para>
/// </summary>
public sealed class SecretMasker
{
    private const string Mask = "***";
    private const int TooShortToBeWorthMasking = 6;

    private readonly IReadOnlyList<string> _values;

    public SecretMasker(AgentSmithConfig config)
    {
        _values = Known(config)
            .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length >= TooShortToBeWorthMasking)
            .Distinct(StringComparer.Ordinal)
            // Longest first: a token that contains a shorter secret must not be left
            // half-masked by the shorter replacement running first.
            .OrderByDescending(v => v.Length)
            .ToList();
    }

    public string Apply(string text)
    {
        if (string.IsNullOrEmpty(text) || _values.Count == 0) return text;
        foreach (var value in _values)
            text = text.Replace(value, Mask, StringComparison.Ordinal);
        return text;
    }

    private static IEnumerable<string> Known(AgentSmithConfig config)
    {
        foreach (var secret in config.Secrets.Values) yield return secret;
        foreach (var registry in config.Registries)
        {
            yield return registry.Token;
        }
    }
}
