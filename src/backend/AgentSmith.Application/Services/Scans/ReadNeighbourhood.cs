using AgentSmith.Application.Services.Handlers;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-03e1: whether the run read anything AROUND a located file — the mechanical
/// half of what it means to have examined a station.
/// <para>
/// "Examined" cannot be a fresh assertion by the model: a scan that says it examined a
/// station is exactly the report this whole track was opened against. So it is derived from
/// two things the run already holds — the station's own citation resolving against the read
/// set, and the read set holding OTHER files beneath the directory that citation names. A
/// scan that opened one class and nothing around it looked at a station; it did not examine
/// one.
/// </para>
/// <para>
/// The directory match is segment-aligned and lenient in the same direction
/// <see cref="ReadPathNormalizer"/> is: read paths arrive sandbox-prefixed while a claim is
/// repo-relative, so a directory counts when it appears as a whole segment run inside the
/// read path. The risk worth avoiding is calling real work unexamined, not the rarer
/// reverse.
/// </para>
/// </summary>
public static class ReadNeighbourhood
{
    /// <summary>True when the run read a file beneath the directory of
    /// <paramref name="file"/> other than that file itself.</summary>
    public static bool HoldsFilesBeneath(IReadOnlyCollection<string>? read, string? file)
    {
        if (read is null || read.Count == 0 || string.IsNullOrWhiteSpace(file)) return false;
        var directory = DirectoryOf(ReadPathNormalizer.Normalize(file));
        var basename = ReadPathNormalizer.Basename(file);
        return read.Any(path =>
            !string.Equals(ReadPathNormalizer.Basename(path), basename, StringComparison.OrdinalIgnoreCase)
            && Beneath(ReadPathNormalizer.Normalize(path), directory));
    }

    private static string DirectoryOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash > 0 ? path[..slash] : string.Empty;
    }

    /// <summary>A file at the repository root has the whole tree beneath it, so any other
    /// read file counts; below the root the directory must appear as a whole segment run.</summary>
    private static bool Beneath(string path, string directory) =>
        directory.Length == 0
        || path.StartsWith($"{directory}/", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"/{directory}/", StringComparison.OrdinalIgnoreCase);
}
