using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06c: a design turn's repositories as an <see cref="IEvidenceProbe"/>. A cited path
/// is routed as the filesystem tools route it — the longest repository name it starts with — because
/// the partner writes paths with and without that prefix, and a rule that ignored either would
/// report a correct citation as missing.
/// <para>
/// Every answer that is not certain is NOT CHECKED: a qualified path (a template, a website, a
/// repository named in a form no tool addresses), a bare path several repositories could hold, a
/// read that could not run. A missed check costs less than a false finding.
/// </para>
/// </summary>
public sealed class SandboxEvidenceProbe : IEvidenceProbe
{
    private readonly IReadOnlyDictionary<string, ISandbox> _repositories;
    private readonly SandboxEvidenceRead _reads;

    public SandboxEvidenceProbe(
        IReadOnlyDictionary<string, ISandbox> repositories, ITurnActivityObserverAccessor activity)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        _repositories = repositories;
        _reads = new SandboxEvidenceRead(activity);
    }

    public async Task<EvidenceProbeResult> ProbeAsync(string? qualifier, string path, CancellationToken ct)
    {
        if (qualifier is not null || _repositories.Count == 0 || string.IsNullOrEmpty(path))
            return new EvidenceProbeResult.NotChecked();
        var (key, matched) = RepoPathRouter.MatchLongestKey(_repositories, path);
        if (matched is not null)
        {
            var stripped = await _reads.ReadAsync(key, matched, Stripped(key, path), ct);
            if (_repositories.Count > 1 || stripped is EvidenceProbeResult.File) return stripped;
            // The tools strip the only repository's name, but its tree may hold a folder named
            // like it: either reading that finds a file is the one the citation meant.
            return Either(stripped, await _reads.ReadAsync(key, matched, path, ct));
        }
        if (_repositories.Count == 1)
        {
            var (name, only) = _repositories.Single();
            return await _reads.ReadAsync(name, only, path, ct);
        }
        return await BareAsync(path, ct);
    }

    /// <summary>A bare path on a multi-repository turn stands only where exactly one holds it.</summary>
    private async Task<EvidenceProbeResult> BareAsync(string path, CancellationToken ct)
    {
        var files = new List<EvidenceProbeResult.File>();
        foreach (var (name, sandbox) in _repositories)
            if (await _reads.ReadAsync(name, sandbox, path, ct) is EvidenceProbeResult.File file) files.Add(file);
        return files.Count == 1 ? files[0] : new EvidenceProbeResult.NotChecked();
    }

    private static EvidenceProbeResult Either(EvidenceProbeResult first, EvidenceProbeResult second) =>
        (first, second) switch
        {
            (EvidenceProbeResult.File, _) => first,
            (_, EvidenceProbeResult.File) => second,
            (EvidenceProbeResult.NotAFile, EvidenceProbeResult.NotAFile) => first,
            _ => new EvidenceProbeResult.NotChecked(),
        };

    private static string Stripped(string key, string path) =>
        key.Length >= path.Length ? "." : path[(key.Length + 1)..] is { Length: > 0 } rest ? rest : ".";
}
