using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-02-3f06b: the repository's own working tree as an evidence probe. A qualified path
/// names another repository this tree does not hold, so it is not checked here.
/// </summary>
internal sealed class FileSystemEvidenceProbe(string root) : IEvidenceProbe
{
    public async Task<EvidenceProbeResult> ProbeAsync(string? qualifier, string path, CancellationToken ct)
    {
        if (qualifier is not null) return new EvidenceProbeResult.NotChecked();
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!File.Exists(full)) return new EvidenceProbeResult.NotAFile();
        return new EvidenceProbeResult.File(EvidenceLineCount.Of(await File.ReadAllTextAsync(full, ct)));
    }
}
