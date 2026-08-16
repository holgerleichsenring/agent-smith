using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: a scanned source the substantiation can read — the files that exist, and
/// nothing else. A citation to anything not in here is exactly the invented location
/// the phase drops.
/// </summary>
internal sealed class ScannedSourceStub(Dictionary<string, string> files)
    : ISandboxFileReader, ISandboxFileReaderFactory
{
    public ISandboxFileReader Create(ISandbox sandbox) => this;

    public Task<string?> TryReadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(files.GetValueOrDefault(path));

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(files.ContainsKey(path));

    public Task<string> ReadRequiredAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(files[path]);

    public Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        files[path] = content;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([.. files.Keys]);
}
