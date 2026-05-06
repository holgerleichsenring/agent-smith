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
        CancellationToken cancellationToken)
    {
        var result = $"Local repository - no PR created, branch pushed: {repository.CurrentBranch}";
        return Task.FromResult(result);
    }
}
