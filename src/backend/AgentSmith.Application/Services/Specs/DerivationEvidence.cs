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
/// <param name="idPrefix">2026-09-15-ffa7: the letter ids are minted under. Two holders in
/// one conversation mint under two letters, or one id names two different looks.</param>
/// <param name="actor">Who the minted sentence says took the look.</param>
public sealed class DerivationEvidence(string idPrefix = "L", string actor = "the derivation")
{
    private readonly Lock _sync = new();
    private readonly List<EvidenceLook> _looks = [];
    private readonly HashSet<string> _once = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Lines
    {
        get { lock (_sync) return [.. _looks.Select(l => l.Line)]; }
    }

    /// <summary>2026-09-15-ffa7: every look taken, structured — the id, what ran, and whether
    /// it reached a verdict.</summary>
    public IReadOnlyList<EvidenceLook> Looks
    {
        get { lock (_sync) return [.. _looks]; }
    }

    /// <summary>Mints the next id and remembers the look under it. <paramref name="ran"/>
    /// is whether the exit code means the tool reached a verdict; a look that did not
    /// says so on its own line.</summary>
    public string Remember(string repository, string what, int exitCode, bool ran)
    {
        var clause = ran ? string.Empty : " and could not run, so it proves nothing";
        lock (_sync)
        {
            var id = $"{idPrefix}{_looks.Count + 1}";
            _looks.Add(new EvidenceLook(id, repository, what, exitCode, ran,
                $"[{id}] {repository}: {actor} ran '{what}' exited {exitCode}{clause}"));
            return id;
        }
    }

    /// <summary>
    /// 2026-09-13-9f84: remembers a look the FRAMEWORK takes on its own, at most once per
    /// derivation. A tool call is minted every time because the model asked every time; the
    /// template's declared proof is read again on each attempt of the retry loop, and four
    /// ids for one unchanged file would be four facts where the run only measured one.
    /// Null when that look is already on the record.
    /// </summary>
    public string? RememberOnce(string repository, string what, int exitCode, bool ran)
    {
        lock (_sync)
        {
            if (!_once.Add($"{repository}|{what}")) return null;
        }
        return Remember(repository, what, exitCode, ran);
    }

    /// <summary>The id a line carries, or null for a line minted by nobody.</summary>
    public static string? IdOf(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var close = line.IndexOf(']', StringComparison.Ordinal);
        return line.StartsWith('[') && close > 1 ? line[1..close] : null;
    }

    /// <summary>2026-09-15-ffa7: the one place an id is turned into the line it names — the
    /// fact resolver, the cut-review admission and its rejection all read through it. The
    /// first line minted under an id wins.</summary>
    public static IReadOnlyDictionary<string, string> IndexById(IEnumerable<string>? evidence)
    {
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in evidence ?? [])
            if (IdOf(line) is { } id)
                byId.TryAdd(id, line);
        return byId;
    }

    /// <summary>2026-09-15-ffa7: every id a citation field names, in order — a model writes
    /// <c>R1</c>, <c>[R1]</c>, <c>R1, R3</c> or a list, and each is a candidate.</summary>
    public static IReadOnlyList<string> CitationsIn(string? cites) =>
        [.. (cites ?? string.Empty)
            .Split([',', ';', ' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeCitation)
            .Where(id => id.Length > 0)];

    /// <summary>What a citation may look like in the model's own spelling — <c>L3</c>,
    /// <c>[L3]</c>, <c>l3</c> — folded to the id as minted.</summary>
    public static string NormalizeCitation(string citation) =>
        (citation ?? string.Empty).Trim().TrimStart('[').TrimEnd(']').Trim().ToUpperInvariant();
}
