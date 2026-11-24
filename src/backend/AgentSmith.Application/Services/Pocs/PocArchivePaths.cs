using AgentSmith.Application.Services.Archives;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: every file's path after the strip fits pocs.archive.max_path_chars and no two files share
/// a path in any case — a case clash checks out as one file on a case-insensitive disk. The shared rule decides.</summary>
public sealed class PocArchivePaths(ReferencePathRule paths)
{
    internal PocRuleRefusal? RefusalOf(IReadOnlyList<PocArchiveItem> items, PocArchiveLimits limits)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(i => !i.IsDirectory)
            .Select(i => paths.RefusalOf(i.Path, seen, limits.MaxPathChars, PocArchiveLimits.MaxPathCharsKey))
            .FirstOrDefault(why => why is not null) is { } refusal
            ? new PocRuleRefusal(PocArchiveChecks.Paths, refusal)
            : null;
    }
}
