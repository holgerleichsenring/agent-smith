using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-db88a: the harness's stub remote for a run's checkout and pull requests, whose checkout-free branch writes
/// and reads go to a <see cref="RecordingBranchSources"/> — so a test sees every file filing put on a branch and the host
/// readers read the branches back, beside the runs that check out, build and open pull requests.
/// </summary>
internal sealed class BranchRecordingSourceFactory(RecordingBranchSources branches) : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config) => new Provider(new StubSourceProvider(), branches.Create(config));

    private sealed class Provider(StubSourceProvider stub, ISourceProvider branch) : ISourceProvider
    {
        public string ProviderType => stub.ProviderType;

        public Task<BranchWriteResult> WriteFilesToBranchAsync(BranchName target, IReadOnlyList<RepoFile> files, string message,
            CancellationToken cancellationToken, BranchName? startFrom = null) =>
            branch.WriteFilesToBranchAsync(target, files, message, cancellationToken, startFrom);

        public Task<string?> TryReadFileAsync(string path, BranchName target, CancellationToken cancellationToken) =>
            branch.TryReadFileAsync(path, target, cancellationToken);

        public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, BranchName target, CancellationToken cancellationToken) =>
            branch.ListDirectoryAsync(path, target, cancellationToken);

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) => stub.ProbeAsync(cancellationToken);

        public Task<Repository> CheckoutAsync(BranchName? target, CancellationToken cancellationToken) =>
            stub.CheckoutAsync(target, cancellationToken);

        public Task<string> CreatePullRequestAsync(Repository repository, string title, string description,
            CancellationToken cancellationToken, PullRequestTicket? linkedTicket = null, bool isDraft = false,
            BranchName? targetBranch = null) =>
            stub.CreatePullRequestAsync(repository, title, description, cancellationToken, linkedTicket, isDraft, targetBranch);

        public Task<string?> FindOpenPullRequestAsync(Repository repository, CancellationToken cancellationToken) =>
            stub.FindOpenPullRequestAsync(repository, cancellationToken);

        public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
            stub.TryReadFileAsync(path, cancellationToken);

        public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
            stub.ListDirectoryAsync(path, cancellationToken);

        public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken) =>
            stub.UpdatePullRequestBodyAsync(prUrl, newBody, cancellationToken);

        public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
            stub.MarkPullRequestReadyAsync(prUrl, cancellationToken);

        public Task<PullRequestCompletion> CompletePullRequestAsync(string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
            stub.CompletePullRequestAsync(prUrl, sourceBranch, cancellationToken);

        public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
            stub.ReadPullRequestBaseAsync(prUrl, cancellationToken);

        public Task<bool> RetargetPullRequestAsync(string prUrl, BranchName target, CancellationToken cancellationToken) =>
            stub.RetargetPullRequestAsync(prUrl, target, cancellationToken);

        public Task<bool> BranchExistsAsync(BranchName target, CancellationToken cancellationToken) =>
            stub.BranchExistsAsync(target, cancellationToken);

        public Task<RepoFileListing> ListFilesUnderAsync(string path, BranchName target, int maxFiles, CancellationToken cancellationToken) =>
            stub.ListFilesUnderAsync(path, target, maxFiles, cancellationToken);

        public Task<BranchName> GetDefaultBranchAsync(CancellationToken cancellationToken) => stub.GetDefaultBranchAsync(cancellationToken);

        public Task<PullRequestCompletion> MergePullRequestAsync(string prUrl, BranchName sourceBranch, BranchProtection protection,
            MergeStyle style, CancellationToken cancellationToken) =>
            stub.MergePullRequestAsync(prUrl, sourceBranch, protection, style, cancellationToken);
    }
}
