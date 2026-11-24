using System.IO.Compression;
using System.Text;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-23-933dd: one file out of an artifact a host hands back as a zip (GitHub, Azure DevOps) — found by its path
/// inside the zip, at its root or under one folder named after the artifact; its declared size is checked before a byte
/// is inflated, so a file over the bound answers too-large with that size. A file the zip does not hold answers null.
/// </summary>
public static class PipelineArtifactZip
{
    public static async Task<PipelineArtifactText?> ReadAsync(Stream zip, PipelineArtifact artifact, long maxBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(artifact);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: false);
        var entry = archive.GetEntry(artifact.File) ?? archive.GetEntry($"{artifact.Name}/{artifact.File}");
        if (entry is null) return null;
        if (entry.Length > maxBytes) return PipelineArtifactText.TooLarge(entry.Length);
        await using var content = entry.Open();
        using var reader = new StreamReader(content, Encoding.UTF8);
        return PipelineArtifactText.Of(await reader.ReadToEndAsync(ct), entry.Length);
    }
}
