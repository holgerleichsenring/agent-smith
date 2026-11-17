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

    /// <summary>2026-11-17-a0b6b: what a branch already held before the test acted — read like a write, never
    /// counted among <see cref="Writes"/>.</summary>
    internal List<BranchWrite> Seeded { get; } = [];

    /// <summary>2026-11-17-a0b6b: puts <paramref name="files"/> on <paramref name="branch"/> of <paramref name="repo"/>.</summary>
    internal void Seed(string repo, string branch, IReadOnlyList<RepoFile> files) =>
        Seeded.Add(new BranchWrite(repo, branch, "seeded", files));

    private IEnumerable<BranchWrite> OnBranch(string repo, string branch) =>
        Seeded.Concat(Writes).Where(w => w.Repo == repo && w.Branch == branch);

    /// <summary>The sha the remote answers; null makes it answer none, which is a failure.</summary>
    internal string? Sha { get; set; } = DefaultSha;

    /// <summary>When set, the write THROWS with it instead of answering — a remote that is down.</summary>
    internal Exception? Throws { get; set; }

    /// <summary>2026-11-17-a0b6a: when set, a branch READ throws with it — a host that cannot be asked.</summary>
    internal Exception? ReadThrows { get; set; }

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
            PullRequestTicket? linkedTicket = null, bool isDraft = false, BranchName? targetBranch = null) =>
            throw new NotSupportedException(Inert);

        public Task<string?> FindOpenPullRequestAsync(
            Repository repository, CancellationToken cancellationToken) =>
            throw new NotSupportedException(Inert);

        // 2026-10-12-6bfaa: no branch beyond the default exists here, so a read falls to the default reads.
        public Task<bool> BranchExistsAsync(BranchName branch, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        // 2026-11-17-a0b6a: a branch answers what was written to it, the newest write of a path winning, so a
        // filed set reads back through the host-side branch read; a read of a branch nothing wrote answers null.
        public Task<string?> TryReadFileAsync(string path, BranchName branch, CancellationToken cancellationToken)
        {
            if (owner.ReadThrows is not null) throw owner.ReadThrows;
            return Task.FromResult(owner.OnBranch(repo, branch.Value).LastOrDefault(w => w.ContentOf(path) is not null)
                ?.ContentOf(path));
        }

        public Task<IReadOnlyList<string>> ListDirectoryAsync(
            string path, BranchName branch, CancellationToken cancellationToken)
        {
            if (owner.ReadThrows is not null) throw owner.ReadThrows;
            var prefix = path.TrimEnd('/') + "/";
            return Task.FromResult<IReadOnlyList<string>>([.. owner.OnBranch(repo, branch.Value)
                .SelectMany(w => w.Paths)
                .Where(p => p.StartsWith(prefix, StringComparison.Ordinal) && !p[prefix.Length..].Contains('/'))
                .Select(p => p[prefix.Length..])
                .Distinct(StringComparer.Ordinal)]);
        }

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
