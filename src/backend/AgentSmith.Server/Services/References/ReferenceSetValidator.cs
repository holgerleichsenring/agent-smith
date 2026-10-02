using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: decides whether an uploaded set is stored, and in what shape. Paths are
/// normalised and the ignore list drops what an operating system left behind FIRST; every
/// remaining path must stay inside the set, and the site files must pass their size and the set's
/// count and size — or the whole set is refused, naming the file and the limit.
/// 2026-10-02-0d72: a file that is not what a website is made of is SKIPPED and named, not a
/// reason to refuse: a real site folder always carries a LICENSE, a .gitignore or a sitemap.xml,
/// and the operator cannot edit their folder to please this check. The set is refused only when
/// no site file remains.
/// </summary>
public sealed class ReferenceSetValidator(
    ReferencePathRule paths, ReferenceIgnoreList ignored, ReferenceFileTypes types)
{
    public ReferenceSetCheck Check(IReadOnlyList<ReferenceUploadPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var kept = parts.Select(p => p with { Path = paths.Normalise(p.Path) })
            .Where(p => !ignored.IsIgnored(p.Path)).ToList();
        if (kept.Count == 0) return ReferenceSetCheck.Refused("The upload holds no file to keep.");
        if (PathRefusal(kept) is { } path) return ReferenceSetCheck.Refused(path);

        var site = kept.Where(p => types.MediaTypeOf(p.Path) is not null).ToList();
        var skipped = kept.Where(p => types.MediaTypeOf(p.Path) is null).Select(p => p.Path).ToList();
        if (site.Count == 0) return ReferenceSetCheck.Refused(NoSiteFile(skipped));
        if (site.Count > ReferenceUploadLimits.MaxFiles)
            return ReferenceSetCheck.Refused(
                $"The set holds {site.Count} files, over the {ReferenceUploadLimits.MaxFiles}-file limit.");
        return SizeRefusal(site) is { } size ? ReferenceSetCheck.Refused(size) : new ReferenceSetCheck(site, null, skipped);
    }

    // Every path, skipped or not, must stay inside the set: one that climbs out is hostile either way.
    private string? PathRefusal(IEnumerable<ReferenceUploadPart> kept)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return kept.Select(p => paths.RefusalOf(p.Path, seen)).FirstOrDefault(why => why is not null);
    }

    private static string? SizeRefusal(IEnumerable<ReferenceUploadPart> site)
    {
        var total = 0L;
        foreach (var part in site)
        {
            total += part.Content.LongLength;
            if (part.Content.LongLength > ReferenceUploadLimits.MaxFileBytes)
                return $"'{part.Path}' is {ReferenceUploadLimits.Megabytes(part.Content.LongLength)}, over the "
                    + $"{ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxFileBytes)} per-file limit.";
            if (total > ReferenceUploadLimits.MaxSetBytes)
                return $"'{part.Path}' takes the set over the {ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxSetBytes)} set limit.";
        }
        return null;
    }

    internal static string NoSiteFile(IReadOnlyList<string> skipped) =>
        $"The upload holds no file a website is made of — {skipped.Count} skipped, such as "
        + $"{string.Join(", ", skipped.Take(3).Select(p => $"'{p}'"))}; allowed: {ReferenceFileTypes.Allowed}.";
}
