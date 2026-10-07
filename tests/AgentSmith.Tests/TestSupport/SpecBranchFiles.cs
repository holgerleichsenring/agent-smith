using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79a: an in-memory ticket branch — the files the series reader would find in a
/// sandbox. Empty by default, which is "no set on the branch".
/// </summary>
internal sealed class SpecBranchFiles : ISandboxFileReader
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    internal string Key { get; set; } = "azdo-1";

    internal void Seed(string path, string content) => _files[path] = content;

    /// <summary>2026-10-06-03c7d: seeds a manifest at series/{base}.yaml and one planned spec per stem.</summary>
    internal void SeedSeries(string seriesBase, string manifestYaml, IReadOnlyDictionary<string, string> specYamlByStem)
    {
        Seed(SeriesPaths.Manifest(seriesBase), manifestYaml);
        foreach (var (stem, yaml) in specYamlByStem)
            Seed(SeriesPaths.Spec(SeriesPaths.Planned, stem), yaml);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(_files.ContainsKey(path));

    public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
        Task.FromResult(_files.TryGetValue(path, out var content) ? content : null);

    public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
        Task.FromResult(_files[path]);

    public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(
            [.. _files.Keys.Where(k => k.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal))]);

    public Task WriteAsync(string path, string content, CancellationToken ct)
    {
        _files[path] = content;
        return Task.CompletedTask;
    }
}
