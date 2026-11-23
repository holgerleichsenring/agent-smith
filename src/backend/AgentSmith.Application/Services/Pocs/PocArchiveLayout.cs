using System.IO.Compression;
using AgentSmith.Application.Services.Archives;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the entries of a PoC archive as the rules see them. The ignore list drops what an operating system
/// left behind FIRST, so a Finder-made ZIP keeps its single top folder; every path must stay inside the archive (the
/// shared hostile-path rule, read before the strip so a leading '..' can never be stripped away); then a single top
/// folder all entries share is stripped, so the Dockerfile of 'my-app/Dockerfile' is at the root.
/// </summary>
public sealed class PocArchiveLayout(ReferencePathRule paths, ReferenceIgnoreList ignored)
{
    internal PocArchiveLayoutResult Read(ZipArchive zip)
    {
        ArgumentNullException.ThrowIfNull(zip);
        var items = zip.Entries
            .Select(e => new PocArchiveItem(Normalised(e), e.FullName.EndsWith('/') || e.FullName.EndsWith('\\'), e.Length, e))
            .Where(i => !ignored.IsIgnored(i.Path)).ToList();
        if (items.Select(i => paths.HostileRefusalOf(i.Path)).FirstOrDefault(why => why is not null) is { } hostile)
            return new PocArchiveLayoutResult([], new PocRuleRefusal(PocArchiveChecks.Paths, hostile));
        return new PocArchiveLayoutResult(Stripped(items), null);
    }

    private string Normalised(ZipArchiveEntry entry)
    {
        var path = paths.Normalise(entry.FullName);
        return path.Length > 1 && path.EndsWith('/') ? path[..^1] : path;
    }

    // The items without the one top folder every file shares, or as they are when there is none.
    private static IReadOnlyList<PocArchiveItem> Stripped(IReadOnlyList<PocArchiveItem> items)
    {
        var files = items.Where(i => !i.IsDirectory).ToList();
        if (files.Count == 0 || files.Any(f => !f.Path.Contains('/'))) return items;
        var top = files[0].Path[..files[0].Path.IndexOf('/')];
        var prefix = top + "/";
        if (items.Any(i => i.Path != top && !i.Path.StartsWith(prefix, StringComparison.Ordinal))) return items;
        return [.. items.Where(i => i.Path != top).Select(i => i with { Path = i.Path[prefix.Length..] })];
    }
}
