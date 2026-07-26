using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0380: a dictionary-backed ISandboxFileReader for memory-store tests —
/// paths are keys, ListAsync enumerates keys under the given directory.
/// </summary>
internal sealed class InMemorySandboxFileReader : ISandboxFileReader
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(Files.ContainsKey(path));

    public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
        Task.FromResult(Files.TryGetValue(path, out var content) ? content : null);

    public async Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
        await TryReadAsync(path, ct) ?? throw new FileNotFoundException(path);

    public Task WriteAsync(string path, string content, CancellationToken ct)
    {
        Files[path] = content;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct)
    {
        var prefix = path.TrimEnd('/') + "/";
        IReadOnlyList<string> matches = Files.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult(matches);
    }
}
