using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Local source provider — assumes the operator has bind-mounted basePath into the sandbox at /work (or is running
/// InProcessSandbox with basePath as workdir). CheckoutAsync is metadata-only. K8s mode is not supported because the
/// operator cannot bind-mount a local disk into a remote pod.
/// </summary>
public sealed class LocalSourceProvider(string basePath, string? defaultBranch = null, ILogger? logger = null)
    : ISourceProvider
{
    private const string FallbackDefaultBranch = "main";

    private readonly LocalBranchFastForward _fastForward = new();
    private readonly LocalBranchCommit _branchCommit = new(logger ?? NullLogger.Instance);
    private readonly LocalRepositoryRead _read = new(basePath);

    public string ProviderType => "Local";

    // No remote to reach: "reachable" means the base path exists on disk, so latency is always zero.
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

        var target = branch ?? new BranchName(DefaultBranch);
        return Task.FromResult(new Repository(target, basePath));
    }

    // 2026-09-13-a284: a local repository has no pull request server, so there is nothing
    // to open against a base and nothing to move. Its CompletePullRequestAsync
    // fast-forwards the CONFIGURED default branch rather than any rung — the promise that
    // the default branch receives nothing until a human merges is a promise about the
    // remote providers, and this is where it is not one.
    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken,
        PullRequestTicket? linkedTicket = null, bool isDraft = false, BranchName? targetBranch = null)
    {
        // p0326: a Local repo without an origin remote is never pushed; the local commit is the result.
        var result = $"Local repository - no PR created, commit recorded on branch: {repository.CurrentBranch}";
        return Task.FromResult(result);
    }

    // p0390: a Local repo has no PR surface, so CommitAndPR falls through to its unchanged create path.
    public Task<string?> FindOpenPullRequestAsync(
        Repository repository, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
        _read.TryReadFileAsync(path, cancellationToken);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(_read.ListDirectory(path));

    public Task<bool> BranchExistsAsync(BranchName branch, CancellationToken cancellationToken) =>
        _read.BranchExistsAsync(branch.Value, cancellationToken);

    public Task<string?> TryReadFileAsync(string path, BranchName branch, CancellationToken cancellationToken) =>
        _read.TryReadFileAsync(path, branch.Value, cancellationToken);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(
        string path, BranchName branch, CancellationToken cancellationToken) =>
        _read.ListDirectoryAsync(path, branch.Value, cancellationToken);

    public Task<RepoFileListing> ListFilesUnderAsync( // 2026-11-23-c81ea
        string path, BranchName branch, int maxFiles, CancellationToken cancellationToken) =>
        _read.ListFilesUnderAsync(path, branch.Value, maxFiles, cancellationToken);

    public Task<BranchName> GetDefaultBranchAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new BranchName(DefaultBranch));

    public Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    // p0393a: a local repository has no pull request, so there is no draft to leave.
    public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    // 2026-09-13-a284: null is "do not move it" — a local repo has no pull request whose base could be read.
    public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<bool> RetargetPullRequestAsync(
        string prUrl, BranchName target, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    // p0490: finishing the work IS moving the default branch onto the branch the run committed; the branch, not the
    // prUrl (CreatePullRequestAsync's sentence), identifies the work.
    public Task<PullRequestCompletion> CompletePullRequestAsync(
        string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
        _fastForward.RunAsync(basePath, sourceBranch.Value, DefaultBranch, cancellationToken);

    public Task<PullRequestCompletion> MergePullRequestAsync( // 2026-11-17-023fa: nothing protects a local branch
        string prUrl, BranchName sourceBranch, BranchProtection protection, CancellationToken cancellationToken) =>
        CompletePullRequestAsync(prUrl, sourceBranch, cancellationToken);

    // 2026-09-22-b6ad: git plumbing against a scratch index, so the operator's working tree and staged changes are
    // untouched; it answers a real commit sha, because the pointer the caller records needs one. 2026-11-24-f9c5b: a start branch.
    public async Task<BranchWriteResult> WriteFilesToBranchAsync(
        BranchName branch, IReadOnlyList<RepoFile> files, string message, CancellationToken cancellationToken, BranchName? startFrom = null) =>
        await _branchCommit.WriteAsync(basePath, branch.Value, startFrom is not null && await BranchExistsAsync(startFrom, cancellationToken)
            ? startFrom.Value : DefaultBranch, files, message, cancellationToken);

    private string DefaultBranch => defaultBranch ?? FallbackDefaultBranch;
}
