using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-09-01-85b2: reads a cited file from ANY of the run's sandboxes, once.
/// <para>
/// The evidence check used to read the single default sandbox while the scan master
/// addressed every repository in the run, so on a multi-repo scan every finding in the
/// second repository resolved against nothing. It also asked for the path exactly as
/// written, while a master cites what its tools showed it — prefix included.
/// </para>
/// <para>
/// Each distinct path is fetched at most once and remembered, including the misses. A
/// sandbox read is a 30-second-budget round trip and the checked set is now every delivered
/// finding, so thirty findings over eight files must cost eight reads, not thirty.
/// </para>
/// </summary>
public sealed class ScanSourceReader(IReadOnlyList<ISandboxFileReader> sandboxes) : IScanSourceReader
{
    private readonly Dictionary<string, string?> _read = new(StringComparer.Ordinal);

    public async Task<string?> TryReadAsync(string citedPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(citedPath);
        var key = CitedPathMatch.Normalize(citedPath);
        if (_read.TryGetValue(key, out var remembered)) return remembered;
        var content = await FirstReadableAsync(key, cancellationToken);
        _read[key] = content;
        return content;
    }

    private async Task<string?> FirstReadableAsync(string path, CancellationToken cancellationToken)
    {
        foreach (var sandbox in sandboxes)
        {
            foreach (var form in CitedPathMatch.Forms(path))
            {
                var content = await sandbox.TryReadAsync(form, cancellationToken);
                if (content is not null) return content;
            }
        }
        return null;
    }
}
