using System.IO.Compression;
using System.Security.Cryptography;
using AgentSmith.Application.Services.Archives;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: a validated revision's files, re-read from the stored archive in process — the same ignore list and
/// top-folder strip as the intake, exactly the manifest's paths, each inflated bounded at its manifest size and checked
/// against its sha256. A missing file or a mismatch is a failure naming the path, so what is scanned and committed is
/// what the intake validated.
/// </summary>
public sealed class PocManifestFiles(PocArchiveLayout layout, BoundedZipInflater inflater)
{
    public (IReadOnlyList<ReferenceSetFile> Files, string? Failure) Read(byte[] zip, string manifestJson)
    {
        ArgumentNullException.ThrowIfNull(zip);
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zip, writable: false), ZipArchiveMode.Read);
            return Collect(archive, PocManifest.FromJson(manifestJson));
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException)
        {
            return ([], $"the stored upload is not a readable ZIP archive any more ({e.Message})");
        }
    }

    private (IReadOnlyList<ReferenceSetFile>, string?) Collect(ZipArchive archive, PocManifest manifest)
    {
        var items = layout.Read(archive).Items.Where(i => !i.IsDirectory).ToDictionary(i => i.Path, StringComparer.Ordinal);
        var files = new List<ReferenceSetFile>();
        foreach (var (path, entry) in manifest.Files)
        {
            if (!items.TryGetValue(path, out var item)) return ([], $"'{path}' of the validated manifest is missing from the stored upload");
            var inflated = inflater.Inflate(item.Entry, entry.Size);
            if (inflated.Fault is not null || Convert.ToHexStringLower(SHA256.HashData(inflated.Bytes)) != entry.Sha256)
                return ([], $"'{path}' of the stored upload does not match the validated manifest's sha256");
            files.Add(new ReferenceSetFile(path, inflated.Bytes));
        }
        return (files, null);
    }
}
