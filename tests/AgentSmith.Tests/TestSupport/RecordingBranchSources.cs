using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-22-b6ad: a source-provider factory whose only live member is the checkout-free branch
/// write — what filing calls, and the one thing a filing test has to see. Every other member is
/// inert: filing opens no pull request and checks nothing out, so one reaching this fake is a test
/// that lost its way.
/// </summary>
internal sealed class RecordingBranchSources : ISourceProviderFactory
{
    internal const string DefaultSha = "9f1c0aa0000000000000000000000000000000ab";

    internal List<BranchWrite> Writes { get; } = [];

    /// <summary>The sha the remote answers; null makes it answer none, which is a failure.</summary>
    internal string? Sha { get; set; } = DefaultSha;

    /// <summary>When set, the write THROWS with it instead of answering — a remote that is down.</summary>
    internal Exception? Throws { get; set; }

    /// <summary>Called as the write lands, so a test can journal WHEN it happened.</summary>
    internal Action? OnWrite { get; set; }

    public ISourceProvider Create(RepoConnection config) =>
        new Provider(this, config?.Name ?? string.Empty);

    internal sealed record BranchWrite(
        string Repo, string Branch, string Message, IReadOnlyList<RepoFile> Files)
    {
        internal string? ContentOf(string path) =>
            Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal))?.Content;

        internal IReadOnlyList<string> Paths => [.. Files.Select(f => f.Path)];
    }

    private sealed class Provider(RecordingBranchSources owner, string repo) : ISourceProvider
    {
        private const string Inert = "A filing writes a branch and nothing else.";

        public string ProviderType => "Recording";

        public Task<BranchWriteResult> WriteFilesToBranchAsync(
            BranchName branch, IReadOnlyList<RepoFile> files, string message,
            CancellationToken cancellationToken)
        {
            if (owner.Throws is not null) throw owner.Throws;
            owner.OnWrite?.Invoke();
            owner.Writes.Add(new BranchWrite(repo, branch.Value, message, files));
            return Task.FromResult(owner.Sha is null
                ? BranchWriteResult.Failed("the remote named no commit")
                : BranchWriteResult.Ok(owner.Sha));
        }

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<string> CreatePullRequestAsync(
            Repository repository, string title, string description, CancellationToken cancellationToken,
            TicketId? linkedTicketId = null, bool isDraft = false, BranchName? targetBranch = null) =>
            throw new NotSupportedException(Inert);

        public Task<string?> FindOpenPullRequestAsync(
            Repository repository, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<IReadOnlyList<string>> ListDirectoryAsync(
            string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<bool> UpdatePullRequestBodyAsync(
            string prUrl, string newBody, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<bool> RetargetPullRequestAsync(
            string prUrl, BranchName target, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        public Task<PullRequestCompletion> CompletePullRequestAsync(
            string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);
    }
}
