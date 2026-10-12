using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-f5f7e: the stub source provider with pull requests that behave like a forge's — one URL
/// per repository (its remote URL plus <c>/pull/1</c>), found again once opened, and every body kept
/// as last written — so a test reads which repository got a pull request and what its body links to.
/// </summary>
internal sealed class PerRepoPullRequestProvider : ISourceProvider, ISourceProviderFactory
{
    private readonly ISourceProvider _inner = new StubSourceProviderFactory().Create(new RepoConnection());
    private readonly Dictionary<string, string> _opened = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);

    /// <summary>Pull request URL → the body it carries now.</summary>
    public IReadOnlyDictionary<string, string> Bodies { get { lock (_bodies) return new Dictionary<string, string>(_bodies); } }

    public ISourceProvider Create(RepoConnection config) => this;

    public string ProviderType => _inner.ProviderType;

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) => _inner.ProbeAsync(cancellationToken);

    public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
        _inner.CheckoutAsync(branch, cancellationToken);

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken, TicketId? linkedTicketId = null, bool isDraft = false,
        BranchName? targetBranch = null)
    {
        var url = $"{repository.RemoteUrl}/pull/1";
        lock (_bodies) { _opened[repository.RemoteUrl] = url; _bodies[url] = description; }
        return Task.FromResult(url);
    }

    public Task<string?> FindOpenPullRequestAsync(Repository repository, CancellationToken cancellationToken)
    {
        lock (_bodies) return Task.FromResult(_opened.TryGetValue(repository.RemoteUrl, out var url) ? url : null);
    }

    public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken)
    {
        lock (_bodies) _bodies[prUrl] = newBody;
        return Task.FromResult(true);
    }

    public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
        _inner.ReadPullRequestBaseAsync(prUrl, cancellationToken);

    public Task<bool> RetargetPullRequestAsync(string prUrl, BranchName target, CancellationToken cancellationToken) =>
        _inner.RetargetPullRequestAsync(prUrl, target, cancellationToken);

    public Task<bool> BranchExistsAsync(BranchName branch, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<string?> TryReadFileAsync(string path, BranchName branch, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, BranchName branch, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
        _inner.TryReadFileAsync(path, cancellationToken);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
        _inner.ListDirectoryAsync(path, cancellationToken);

    public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
        _inner.MarkPullRequestReadyAsync(prUrl, cancellationToken);

    public Task<PullRequestCompletion> CompletePullRequestAsync(
        string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
        _inner.CompletePullRequestAsync(prUrl, sourceBranch, cancellationToken);

    public Task<BranchWriteResult> WriteFilesToBranchAsync(
        BranchName branch, IReadOnlyList<RepoFile> files, string message, CancellationToken cancellationToken) =>
        _inner.WriteFilesToBranchAsync(branch, files, message, cancellationToken);
}
