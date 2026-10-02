using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: decides whether an uploaded set is stored, and in what shape. Paths are
/// normalised and the ignore list drops what an operating system left behind FIRST.
/// 2026-10-02-075da: every other file is KEPT unless <see cref="ReferenceKeepRule"/> names a
/// reason to leave it out — a .py, a .env, a Dockerfile is what tells the model what the upload
/// is. Every path must stay inside the set; a kept one must also fit the column and be unique; the
/// kept files must fit the set's bounds, or the whole set is refused naming its largest entries.
/// </summary>
public sealed class ReferenceSetValidator(ReferencePathRule paths, ReferenceIgnoreList ignored)
{
    public ReferenceSetCheck Check(IReadOnlyList<ReferenceUploadPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var candidates = parts.Select(p => p with { Path = paths.Normalise(p.Path) })
            .Where(p => !ignored.IsIgnored(p.Path)).ToList();
        if (candidates.Count == 0) return ReferenceSetCheck.Refused(NothingToKeep);
        if (candidates.Select(p => paths.HostileRefusalOf(p.Path)).FirstOrDefault(why => why is not null) is { } hostile)
            return ReferenceSetCheck.Refused(hostile);

        var leftOut = candidates.Select(p => (Part: p, Why: ReferenceKeepRule.LeftOut(p.Path, p.Content.LongLength))).ToList();
        var kept = leftOut.Where(e => e.Why is null).Select(e => e.Part).ToList();
        var named = ReferenceKeepRule.Collapsed(leftOut.Select(e => e.Why).OfType<ReferenceLeftOut>());
        if (kept.Count == 0) return ReferenceSetCheck.Refused(NothingKept(named));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (kept.Select(p => paths.RefusalOf(p.Path, seen)).FirstOrDefault(why => why is not null) is { } path)
            return ReferenceSetCheck.Refused(path);
        return ReferenceSetBounds.RefusalOf(kept) is { } bound
            ? ReferenceSetCheck.Refused(bound)
            : new ReferenceSetCheck(kept, null, named);
    }

    internal const string NothingToKeep = "The upload holds no file to keep.";

    internal static string NothingKept(IReadOnlyList<ReferenceLeftOut> leftOut) =>
        $"The upload holds no file to keep — all {leftOut.Count} entries were left out, such as "
        + $"{string.Join(", ", leftOut.Take(3).Select(e => $"'{e.Path}' ({e.Reason})"))}.";
}
