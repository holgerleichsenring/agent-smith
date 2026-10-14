namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762e: the one rule that decides whether a reviewer's file:line rests on a read the
/// FRAMEWORK minted — a read of its own that exited 0, of exactly that repository and that path, with the
/// line inside what the read returned. Lifted out of <see cref="SpecReviewAdmission"/> so the spec diff
/// review and the spec attack review keep one rule rather than two that drift.
/// </summary>
internal static class OwnReadRule
{
    /// <summary>The look's reads that exited 0, by id — the only reads a line can rest on.</summary>
    public static IReadOnlyDictionary<string, EvidenceLook> Reads(DerivationLook? look) =>
        (look?.Evidence.Looks ?? [])
            .Where(l => l.Ran && l.ExitCode == 0 && l.Kind == EvidenceRecord.Read)
            .ToDictionary(l => l.Id, l => l, StringComparer.Ordinal);

    /// <summary>The first id <paramref name="cites"/> names that is a read of exactly this repository and
    /// path, or null.</summary>
    public static EvidenceLook? Read(
        string? cites, string repository, string path, IReadOnlyDictionary<string, EvidenceLook> reads) =>
        DerivationEvidence.CitationsIn(cites)
            .Select(id => reads.TryGetValue(id, out var read) ? read : null)
            .FirstOrDefault(read => read is not null && Same(read, repository, path));

    /// <summary>True when the line lies inside what the read returned.</summary>
    public static bool Within(int line, EvidenceLook read) => line >= 1 && line <= read.LinesReturned;

    /// <summary>True when the look named this repository and, normalised, this path.</summary>
    public static bool Same(EvidenceLook look, string repository, string? path) =>
        string.Equals(look.Repository, repository, StringComparison.Ordinal)
        && string.Equals(Normalize(look.Path), Normalize(path), StringComparison.Ordinal);

    // The read tool contains a path to its "./x" form; a diff spells it "x". One spelling, or
    // an honest finding on the file it really read is discarded for a leading dot.
    public static string Normalize(string? path)
    {
        var text = (path ?? string.Empty).Trim().Replace('\\', '/');
        while (text.StartsWith("./", StringComparison.Ordinal)) text = text[2..];
        return text.TrimStart('/');
    }
}
