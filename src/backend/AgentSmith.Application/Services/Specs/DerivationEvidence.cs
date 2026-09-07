namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: what the derivation looked at, one line per tool call, minted by the
/// framework — the same grammar <see cref="SearchEvidence"/> gives the account, with an
/// ID in front: <c>[L3] api: the derivation ran 'grep …' exited 0</c>.
/// <para>
/// The id is the whole point. A fact the model states cites an id, and a reader resolves
/// the id against THESE lines — so "this is a fact" is decided by whether the framework
/// minted the id, never by the model labelling its own sentence. An exit that says the
/// tool could not run keeps its line and says so in words, because dropping it would
/// refuse a derivation for citing a look it really took, and leaving it silent would let a
/// broken audit prove that nothing is vulnerable.
/// </para>
/// </summary>
public sealed class DerivationEvidence
{
    private const string IdPrefix = "L";
    private readonly Lock _sync = new();
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get { lock (_sync) return [.. _lines]; }
    }

    /// <summary>Mints the next id and remembers the look under it. <paramref name="ran"/>
    /// is whether the exit code means the tool reached a verdict; a look that did not
    /// says so on its own line.</summary>
    public string Remember(string repository, string what, int exitCode, bool ran)
    {
        var clause = ran ? string.Empty : " and could not run, so it proves nothing";
        lock (_sync)
        {
            var id = $"{IdPrefix}{_lines.Count + 1}";
            _lines.Add($"[{id}] {repository}: the derivation ran '{what}' exited {exitCode}{clause}");
            return id;
        }
    }

    /// <summary>The id a line carries, or null for a line minted by nobody.</summary>
    public static string? IdOf(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var close = line.IndexOf(']', StringComparison.Ordinal);
        return line.StartsWith('[') && close > 1 ? line[1..close] : null;
    }

    /// <summary>What a citation may look like in the model's own spelling — <c>L3</c>,
    /// <c>[L3]</c>, <c>l3</c> — folded to the id as minted.</summary>
    public static string NormalizeCitation(string citation) =>
        (citation ?? string.Empty).Trim().TrimStart('[').TrimEnd(']').Trim().ToUpperInvariant();
}
