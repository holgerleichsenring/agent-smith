using System.IO.Compression;
using System.Security.Cryptography;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: reads an uploaded PoC archive on the server; nothing of it executes. <see cref="Admit"/> reads
/// the central directory only (plus the root Dockerfile, inflated with a counted bound) and runs every rule declared
/// data answers; <see cref="Intake"/> inflates every file counted and checksummed, runs every rule again on what was
/// inflated, the binary bound with them, and writes the manifest. Both apply the ignore list, then the top-folder
/// strip, then the rules.
/// </summary>
public sealed class PocArchiveReader(
    PocArchiveLayout layout, PocArchiveRules rules, PocRootDockerfile dockerfile, PocArchiveInflater inflater)
{
    public PocArchiveAdmission Admit(byte[] zip, PocArchiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(limits);
        return Read(zip, archive =>
        {
            var read = layout.Read(archive);
            var refusal = read.Refusal ?? rules.RefusalOf(read.Items, zip.LongLength, limits);
            if (refusal is not null) return PocArchiveAdmission.Refused(refusal);
            var check = dockerfile.Check(read.Items, limits);
            return check.Refusal is { } refused
                ? PocArchiveAdmission.Refused(refused)
                : new PocArchiveAdmission(null, null, Entries(read.Items), check.Dockerfile);
        }, PocArchiveAdmission.Refused);
    }

    public PocArchiveIntake Intake(byte[] zip, PocArchiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(limits);
        return Read(zip, archive =>
        {
            var read = layout.Read(archive);
            if ((read.Refusal ?? rules.RefusalOf(read.Items, zip.LongLength, limits)) is { } declared)
                return PocArchiveIntake.Refused(declared);
            var (files, refusal) = inflater.Inflate(read.Items, limits);
            var check = refusal is null ? dockerfile.Check(files, limits) : new PocDockerfileCheck(refusal, null);
            var why = check.Refusal ?? rules.RefusalOf(Inflated(read.Items, files), zip.LongLength, limits)
                ?? rules.BinaryRefusalOf(files, limits);
            return why is null ? new PocArchiveIntake(null, null, Manifest(files), check.Dockerfile) : PocArchiveIntake.Refused(why);
        }, PocArchiveIntake.Refused);
    }

    private static T Read<T>(byte[] zip, Func<ZipArchive, T> read, Func<PocRuleRefusal, T> refused)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zip, writable: false), ZipArchiveMode.Read);
            return read(archive);
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException)
        {
            return refused(new PocRuleRefusal(PocArchiveChecks.Paths, $"the upload is not a readable ZIP archive ({e.Message})"));
        }
    }

    private static IReadOnlyList<PocArchiveEntry> Entries(IReadOnlyList<PocArchiveItem> items) =>
        [.. items.Where(i => !i.IsDirectory).Select(i => new PocArchiveEntry(i.Path, i.Entry.Crc32, i.Size))];

    // The items with the sizes actually inflated, so the bounds and ratios read what the archive holds.
    private static IReadOnlyList<PocArchiveItem> Inflated(IReadOnlyList<PocArchiveItem> items, IReadOnlyList<PocInflatedFile> files)
    {
        var sizes = files.ToDictionary(f => f.Path, f => f.Bytes.LongLength, StringComparer.Ordinal);
        return [.. items.Select(i => sizes.TryGetValue(i.Path, out var size) && !i.IsDirectory ? i with { Size = size } : i)];
    }

    private static PocManifest Manifest(IReadOnlyList<PocInflatedFile> files) =>
        new(files.ToDictionary(f => f.Path,
            f => new PocManifestEntry(Convert.ToHexStringLower(SHA256.HashData(f.Bytes)), f.Crc32, f.Bytes.LongLength),
            StringComparer.Ordinal));
}
