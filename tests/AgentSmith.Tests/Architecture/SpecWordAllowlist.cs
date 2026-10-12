namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-12-967ag: where the word phase may still stand, and why.
/// <para>
/// <c>spec-word-allowlist.tsv</c> holds every token that keeps the word, each with a category
/// and a one-line reason. A token whose meaning the token itself carries
/// (<c>SkillExecutionPhase</c>, <c>phase_review</c>, <c>PHASE_GATE_LOG</c>) may stand anywhere.
/// A token that cannot tell its meanings apart — the bare word, and <c>PhaseId</c>, which a
/// relapse would write first — is <c>listed</c>: it may stand only in the files
/// <c>spec-word-files.tsv</c> names, at most as often as it says.
/// </para>
/// <para>
/// Both lists only shrink. A token that occurs nowhere, or a file row whose token left the
/// file, is stale and fails, so the slices that rename stored names (967ab) and drop the
/// readers (967ah) delete rows rather than leave them behind.
/// </para>
/// </summary>
internal sealed class SpecWordAllowlist
{
    private const string Listed = "listed";

    private SpecWordAllowlist(
        IReadOnlyDictionary<string, string> reach, IReadOnlyDictionary<(string Path, string Token), int> files)
    {
        Reach = reach;
        FileRows = files;
    }

    /// <summary>Token → <c>anywhere</c> or <c>listed</c>.</summary>
    public IReadOnlyDictionary<string, string> Reach { get; }

    public IReadOnlyDictionary<(string Path, string Token), int> FileRows { get; }

    public static SpecWordAllowlist Load() => new(
        Rows("spec-word-allowlist.tsv").ToDictionary(row => row[0], row => row[1], StringComparer.Ordinal),
        Rows("spec-word-files.tsv").ToDictionary(row => (row[0], row[1]), row => int.Parse(row[2])));

    public IEnumerable<string> Offenders(IEnumerable<SpecWordFile> files) =>
        from file in files
        from token in file.Tokens
        let offence = Judge(file.Path, token.Key, token.Value)
        where offence is not null
        orderby file.Path, token.Key
        select $"{file.Path}: {token.Key} — {offence}";

    public IEnumerable<string> Stale(IReadOnlyList<SpecWordFile> files)
    {
        var standing = files.SelectMany(file => file.Tokens.Keys).ToHashSet(StringComparer.Ordinal);
        var counts = files
            .SelectMany(file => file.Tokens.Select(token => ((file.Path, token.Key), token.Value)))
            .ToDictionary(entry => entry.Item1, entry => entry.Value);
        return Reach.Keys.Where(token => !standing.Contains(token))
            .Select(token => $"spec-word-allowlist.tsv: {token} occurs nowhere")
            .Concat(FileRows.Keys.Where(row => !counts.ContainsKey(row))
                .Select(row => $"spec-word-files.tsv: {row.Path} no longer says {row.Token}"))
            .Concat(FileRows.Keys.Where(row => Reach.GetValueOrDefault(row.Token) != Listed)
                .Select(row => $"spec-word-files.tsv: {row.Token} is not a listed token"))
            .Order(StringComparer.Ordinal);
    }

    private string? Judge(string path, string token, int count)
    {
        if (!Reach.TryGetValue(token, out var reach)) return "not allowlisted";
        if (reach != Listed) return null;
        if (!FileRows.TryGetValue((path, token), out var allowed)) return "allowed only in the files spec-word-files.tsv lists";
        return count > allowed ? $"{count} occurrences, {allowed} allowed" : null;
    }

    private static IEnumerable<string[]> Rows(string file) =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", file))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'));
}
