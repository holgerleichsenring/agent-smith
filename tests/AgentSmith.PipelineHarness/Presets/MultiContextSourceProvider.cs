using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// The stub repository with several contexts instead of one — run 8688's shape (a
/// `frontend` and a `backend` context sharing one toolchain image, hence one sandbox).
/// Lists the given context names; every other call goes to the stub source provider.
/// An optional file override answers <see cref="ISourceProvider.TryReadFileAsync"/> first,
/// so a case can give each context its own context.yaml.
/// </summary>
internal sealed class MultiContextSourceProviderFactory(
    IReadOnlyList<string> contexts, Func<string, string?>? fileOverride = null) : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config) =>
        new MultiContextSourceProvider(contexts, fileOverride);
}

internal sealed class MultiContextSourceProvider(
    IReadOnlyList<string> contexts, Func<string, string?>? fileOverride) : ISourceProvider
{
    private readonly ISourceProvider _inner = new StubSourceProviderFactory().Create(new RepoConnection());

    public string ProviderType => _inner.ProviderType;

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
        path == ".agentsmith/contexts"
            ? Task.FromResult(contexts)
            : _inner.ListDirectoryAsync(path, cancellationToken);

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) => _inner.ProbeAsync(cancellationToken);

    public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
        _inner.CheckoutAsync(branch, cancellationToken);

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken, TicketId? linkedTicketId = null, bool isDraft = false) =>
        _inner.CreatePullRequestAsync(repository, title, description, cancellationToken, linkedTicketId, isDraft);

    public Task<string?> FindOpenPullRequestAsync(Repository repository, CancellationToken cancellationToken) =>
        _inner.FindOpenPullRequestAsync(repository, cancellationToken);

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
        fileOverride?.Invoke(path) is { } content
            ? Task.FromResult<string?>(content)
            : _inner.TryReadFileAsync(path, cancellationToken);

    public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken) =>
        _inner.UpdatePullRequestBodyAsync(prUrl, newBody, cancellationToken);

    public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
        _inner.MarkPullRequestReadyAsync(prUrl, cancellationToken);

    public Task<PullRequestCompletion> CompletePullRequestAsync(
        string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
        _inner.CompletePullRequestAsync(prUrl, sourceBranch, cancellationToken);
}
