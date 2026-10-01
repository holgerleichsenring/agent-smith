using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: decides whether an uploaded set is stored, and in what shape. Paths are
/// normalised and the ignore list drops what an operating system left behind FIRST; then every
/// remaining file must pass — its path, its extension, its size, and the set's count and size —
/// or the whole set is refused, naming the file and the limit.
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
        if (kept.Count > ReferenceUploadLimits.MaxFiles)
            return ReferenceSetCheck.Refused(
                $"The set holds {kept.Count} files, over the {ReferenceUploadLimits.MaxFiles}-file limit.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = 0L;
        foreach (var part in kept)
        {
            total += part.Content.LongLength;
            if (RefusalOf(part, seen, total) is { } why) return ReferenceSetCheck.Refused(why);
        }

        return new ReferenceSetCheck(kept, null);
    }

    private string? RefusalOf(ReferenceUploadPart part, ISet<string> seen, long total)
    {
        if (paths.RefusalOf(part.Path, seen) is { } path) return path;
        if (types.MediaTypeOf(part.Path) is null)
            return $"'{part.Path}' is not a file a website is made of; allowed: {ReferenceFileTypes.Allowed}.";
        if (part.Content.LongLength > ReferenceUploadLimits.MaxFileBytes)
            return $"'{part.Path}' is {ReferenceUploadLimits.Megabytes(part.Content.LongLength)}, over the "
                + $"{ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxFileBytes)} per-file limit.";
        return total > ReferenceUploadLimits.MaxSetBytes
            ? $"'{part.Path}' takes the set over the {ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxSetBytes)} set limit."
            : null;
    }
}
