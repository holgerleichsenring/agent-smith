using System.IO.Compression;
using System.Text.Json;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-08-28-2af6: a copy of an archive whose manifest has been tampered with, so the
/// import's refusals can be provoked without a second database. Nothing in production
/// writes an archive this way — an operator editing an archive is exactly what the format
/// does not support, and this is how the tests prove the import notices.
/// </summary>
internal static class RewrittenArchive
{
    internal static MemoryStream WithManifest(
        Stream original, Func<DataArchiveManifest, DataArchiveManifest> change)
    {
        original.Position = 0;
        using var source = new ZipArchive(original, ZipArchiveMode.Read, leaveOpen: true);
        var rewritten = new MemoryStream();
        using (var target = new ZipArchive(rewritten, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = change(Manifest(source));
            Write(target, DataArchiveFormat.ManifestEntry, JsonSerializer.Serialize(manifest));
            foreach (var entry in source.Entries.Where(e => e.FullName != DataArchiveFormat.ManifestEntry))
                Copy(entry, target);
        }

        rewritten.Position = 0;
        return rewritten;
    }

    /// <summary>An archive with no manifest at all — a zip that is not a data archive.</summary>
    internal static MemoryStream WithoutManifest()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            Write(zip, DataArchiveFormat.EntryFor("Runs"), string.Empty);
        stream.Position = 0;
        return stream;
    }

    private static DataArchiveManifest Manifest(ZipArchive source)
    {
        using var stream = source.GetEntry(DataArchiveFormat.ManifestEntry)!.Open();
        return JsonSerializer.Deserialize<DataArchiveManifest>(stream)!;
    }

    private static void Copy(ZipArchiveEntry entry, ZipArchive target)
    {
        using var from = entry.Open();
        using var to = target.CreateEntry(entry.FullName).Open();
        from.CopyTo(to);
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}
