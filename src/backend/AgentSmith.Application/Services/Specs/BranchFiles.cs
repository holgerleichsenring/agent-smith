using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-a0b6a: one branch of one repository as a read-only file source, through the host's own
/// API — no checkout. A listing answers repository-relative paths as a checkout's does; the host
/// answers names.
/// </summary>
public sealed class BranchFiles(ISourceProvider source, BranchName branch) : ISandboxFileReader
{
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
        await TryReadAsync(path, cancellationToken) is not null;

    public Task<string?> TryReadAsync(string path, CancellationToken cancellationToken) =>
        source.TryReadFileAsync(path, branch, cancellationToken);

    public async Task<string> ReadRequiredAsync(string path, CancellationToken cancellationToken) =>
        await TryReadAsync(path, cancellationToken)
        ?? throw new FileNotFoundException($"{path} is not on branch {branch.Value}", path);

    public Task WriteAsync(string path, string content, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"A branch read writes nothing; {path} was not written.");

    public async Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken cancellationToken) =>
        [.. (await source.ListDirectoryAsync(path, branch, cancellationToken))
            .Select(name => $"{path.TrimEnd('/')}/{SeriesPaths.FileName(name)}")];
}
