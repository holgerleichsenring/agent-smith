using AgentSmith.Contracts.Services;

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
/// <para>
/// 2026-10-01-7f7aa: the values are read at mask time from <see cref="ISecretValues"/>, not
/// frozen from the configuration loaded at startup — a secret added in the Studio later was
/// otherwise never masked. The ordered list is rebuilt only when the source list changes.
/// </para>
/// </summary>
public sealed class SecretMasker(ISecretValues secrets)
{
    private const string Mask = "***";
    private const int TooShortToBeWorthMasking = 6;

    private Snapshot _snapshot = new([], []);

    public string Apply(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var value in Maskable())
            text = text.Replace(value, Mask, StringComparison.Ordinal);
        return text;
    }

    private IReadOnlyList<string> Maskable()
    {
        var source = secrets.All();
        var snapshot = _snapshot;
        if (ReferenceEquals(snapshot.Source, source)) return snapshot.Values;
        _snapshot = snapshot = new Snapshot(source, Order(source));
        return snapshot.Values;
    }

    private static IReadOnlyList<string> Order(IReadOnlyList<string> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length >= TooShortToBeWorthMasking)
            .Distinct(StringComparer.Ordinal)
            // Longest first: a token that contains a shorter secret must not be left
            // half-masked by the shorter replacement running first.
            .OrderByDescending(v => v.Length)
            .ToList();

    private sealed record Snapshot(IReadOnlyList<string> Source, IReadOnlyList<string> Values);
}
