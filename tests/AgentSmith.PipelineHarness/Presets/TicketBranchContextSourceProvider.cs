using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-6bfaa: a repository whose default branch declares NO context and whose ticket branch
/// carries one — the live shape of an init pull request that was never merged. Every other call
/// goes to the stub source provider.
/// </summary>
internal sealed class TicketBranchContextSourceProviderFactory(string ticketBranch, string contextYaml)
    : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config) =>
        new TicketBranchContextSourceProvider(ticketBranch, contextYaml);
}

internal sealed class TicketBranchContextSourceProvider(string ticketBranch, string contextYaml) : ISourceProvider
{
    private const string Contexts = ".agentsmith/contexts";
    private readonly ISourceProvider _inner = new StubSourceProviderFactory().Create(new RepoConnection());

    public string ProviderType => _inner.ProviderType;

    public Task<bool> BranchExistsAsync(BranchName branch, CancellationToken cancellationToken) =>
        Task.FromResult(branch.Value == ticketBranch);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(
        string path, BranchName branch, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(branch.Value == ticketBranch && path == Contexts ? ["default"] : []);

    public Task<string?> TryReadFileAsync(string path, BranchName branch, CancellationToken cancellationToken) =>
        Task.FromResult(branch.Value == ticketBranch && path == $"{Contexts}/default/context.yaml" ? contextYaml : null);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
        path == Contexts ? Task.FromResult<IReadOnlyList<string>>([]) : _inner.ListDirectoryAsync(path, cancellationToken);

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
        path.StartsWith(Contexts, StringComparison.Ordinal)
            ? Task.FromResult<string?>(null)
            : _inner.TryReadFileAsync(path, cancellationToken);

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) => _inner.ProbeAsync(cancellationToken);

    public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
        _inner.CheckoutAsync(branch, cancellationToken);

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken, TicketId? linkedTicketId = null, bool isDraft = false,
        BranchName? targetBranch = null) =>
        _inner.CreatePullRequestAsync(
            repository, title, description, cancellationToken, linkedTicketId, isDraft, targetBranch);

    public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
        _inner.ReadPullRequestBaseAsync(prUrl, cancellationToken);

    public Task<bool> RetargetPullRequestAsync(string prUrl, BranchName target, CancellationToken cancellationToken) =>
        _inner.RetargetPullRequestAsync(prUrl, target, cancellationToken);

    public Task<string?> FindOpenPullRequestAsync(Repository repository, CancellationToken cancellationToken) =>
        _inner.FindOpenPullRequestAsync(repository, cancellationToken);

    public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken) =>
        _inner.UpdatePullRequestBodyAsync(prUrl, newBody, cancellationToken);

    public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
        _inner.MarkPullRequestReadyAsync(prUrl, cancellationToken);

    public Task<PullRequestCompletion> CompletePullRequestAsync(
        string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
        _inner.CompletePullRequestAsync(prUrl, sourceBranch, cancellationToken);

    public Task<BranchWriteResult> WriteFilesToBranchAsync(
        BranchName branch, IReadOnlyList<RepoFile> files, string message, CancellationToken cancellationToken) =>
        _inner.WriteFilesToBranchAsync(branch, files, message, cancellationToken);
}
