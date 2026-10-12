using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Repositories;

/// <summary>
/// 2026-10-12-f5f7c: a repository set known when it is built — the repositories a post-execute
/// pass changed, or the one a bootstrap round works in. Every one of them carries a change by
/// construction, so each lands.
/// </summary>
public sealed class FixedRunRepositories(IReadOnlyDictionary<string, ISandboxFileReader> files)
    : IRunRepositories
{
    public IReadOnlyList<string> Names() => [.. files.Keys];

    public ISandboxFileReader? FilesOf(string repository) =>
        files.TryGetValue(repository, out var reader) ? reader : null;

    public Task<bool> HasCodeChangeAsync(string repository, CancellationToken cancellationToken) =>
        Task.FromResult(files.ContainsKey(repository));
}
