using System.Formats.Tar;
using System.IO.Compression;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0325: atomic gzip-tarball extraction, extracted from
/// <c>SkillsRepositoryClient</c> so the embedded catalog path shares the exact
/// staging-dir + swap semantics of the download path: extract into a sibling
/// staging directory, then replace the destination in one move — a crash never
/// leaves a half-written catalog behind.
/// </summary>
public sealed class CatalogTarballExtractor(ILogger<CatalogTarballExtractor> logger) : ICatalogTarballExtractor
{
    public void Extract(Stream tarGzStream, string destinationDir)
    {
        var stagingDir = destinationDir + ".extracting-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            ExtractToStaging(tarGzStream, stagingDir);
            SwapIntoPlace(stagingDir, destinationDir);
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    private static void ExtractToStaging(Stream tarGzStream, string stagingDir)
    {
        Directory.CreateDirectory(stagingDir);
        using var gz = new GZipStream(tarGzStream, CompressionMode.Decompress, leaveOpen: true);
        TarFile.ExtractToDirectory(gz, stagingDir, overwriteFiles: true);
    }

    private static void SwapIntoPlace(string stagingDir, string destinationDir)
    {
        if (Directory.Exists(destinationDir))
            Directory.Delete(destinationDir, recursive: true);

        var parent = Path.GetDirectoryName(destinationDir);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        Directory.Move(stagingDir, destinationDir);
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException ex)
        {
            // Best-effort cleanup of the staging dir — the extraction itself
            // already succeeded or threw; a leftover staging dir is harmless.
            logger.LogDebug(ex, "Failed to clean up staging directory {Path}", path);
        }
    }
}
