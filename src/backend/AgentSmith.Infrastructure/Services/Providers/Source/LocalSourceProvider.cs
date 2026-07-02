using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Local source provider — assumes the operator has bind-mounted basePath into
/// the sandbox at /work (or is running InProcessSandbox with basePath as workdir).
/// CheckoutAsync is metadata-only. K8s mode is not supported because the
/// operator cannot bind-mount a local disk into a remote pod.
/// </summary>
public sealed class LocalSourceProvider(string basePath) : ISourceProvider
{
    public string ProviderType => "Local";

    // A local repo has no remote to reach — "reachable" means the base path exists
    // on disk. No network round-trip, so latency is always zero.
    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Directory.Exists(basePath)
            ? ConnectionProbeResult.Reachable(0)
            : ConnectionProbeResult.Unreachable(0, $"Local path not found: {basePath}"));

    public Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null)
            throw new NotSupportedException(
                "LocalSourceProvider in Kubernetes is not supported — use bind-mounted DockerSandbox or a remote source provider.");

        var target = branch ?? new BranchName("main");
        return Task.FromResult(new Repository(target, basePath));
    }

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null)
    {
        var result = $"Local repository - no PR created, branch pushed: {repository.CurrentBranch}";
        return Task.FromResult(result);
    }

    public async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var full = Path.Combine(basePath, path);
        if (!File.Exists(full)) return null;
        return await File.ReadAllTextAsync(full, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        var full = Path.Combine(basePath, path);
        if (!Directory.Exists(full))
            return Task.FromResult<IReadOnlyList<string>>([]);
        IReadOnlyList<string> names = Directory.EnumerateFileSystemEntries(full)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();
        return Task.FromResult(names);
    }

    public Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
