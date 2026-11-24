using System.IO.Compression;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: one entry of a PoC archive after the ignore list and the top-folder strip — its path
/// then, whether it is a folder, and its sizes (declared until the intake inflates it).</summary>
internal sealed record PocArchiveItem(string Path, bool IsDirectory, long Size, ZipArchiveEntry Entry)
{
    public long CompressedSize => Entry.CompressedLength;
}
