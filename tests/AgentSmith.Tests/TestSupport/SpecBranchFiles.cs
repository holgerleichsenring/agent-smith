using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79a: an in-memory ticket branch — the files the spec-set reader would find in a
/// sandbox. Empty by default, which is "no set on the branch".
/// </summary>
internal sealed class SpecBranchFiles : ISandboxFileReader
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    internal string Key { get; set; } = "azdo-1";

    internal void Seed(string path, string content) => _files[path] = content;

    /// <summary>Seeds a set.yaml and one file per phase stem under this branch's spec directory.</summary>
    internal void SeedSet(string setYaml, IReadOnlyDictionary<string, string> phaseYamlByStem)
    {
        Seed($".agentsmith/specs/{Key}/set.yaml", setYaml);
        foreach (var (stem, yaml) in phaseYamlByStem)
            Seed($".agentsmith/specs/{Key}/{stem}.yaml", yaml);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(_files.ContainsKey(path));

    public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
        Task.FromResult(_files.TryGetValue(path, out var content) ? content : null);

    public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
        Task.FromResult(_files[path]);

    public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(
            [.. _files.Keys.Where(k => k.StartsWith(path, StringComparison.Ordinal))]);

    public Task WriteAsync(string path, string content, CancellationToken ct)
    {
        _files[path] = content;
        return Task.CompletedTask;
    }
}
