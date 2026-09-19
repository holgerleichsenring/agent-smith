using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-19-4c1f: an ISourceProvider whose REMOTE reads are scripted per repository and
/// per path. It exists beside <see cref="StubSourceProvider"/> rather than widening it:
/// nine test files and the composition harness read that one's fixed answers, and the
/// third absence this phase has to tell apart — a file that could not be READ — can only
/// be produced by a read that THROWS, which no shared stub can be taught without changing
/// what every one of those tests sees.
/// <para>
/// Three of the interface's members are served — the contexts listing and a file read that
/// answers content, answers nothing, or throws. The rest are inert: a design turn writes
/// nothing, so a checkout or a pull request reaching this fake is a test that lost its way.
/// </para>
/// </summary>
internal sealed class ScriptedSourceProvider(
    IReadOnlyList<string> contexts,
    IReadOnlyDictionary<string, string> files,
    IReadOnlySet<string> refused,
    Exception? listingFailure = null) : ISourceProvider
{
    public string ProviderType => "Scripted";

    /// <summary>The paths this provider was asked for, in order — absence has to be
    /// distinguishable from never having looked.</summary>
    public List<string> Reads { get; } = [];

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (listingFailure is not null) throw listingFailure;
        return Task.FromResult(path == ".agentsmith/contexts" ? contexts : []);
    }

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        Reads.Add(path);
        if (refused.Contains(path))
            throw new InvalidOperationException($"401 reading {path}");
        return Task.FromResult(files.GetValueOrDefault(path));
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
        Repository repository, CancellationToken cancellationToken) => throw new NotSupportedException(Inert);

    public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Inert);

    public Task<bool> RetargetPullRequestAsync(
        string prUrl, BranchName target, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Inert);

    public Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Inert);

    public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Inert);

    public Task<PullRequestCompletion> CompletePullRequestAsync(
        string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
        throw new NotSupportedException(Inert);

    private const string Inert = "A design turn writes nothing; this member is not served.";
}

/// <summary>
/// 2026-09-19-4c1f: hands out one <see cref="ScriptedSourceProvider"/> per repository NAME,
/// so one test can put a readable repository, an unreadable one and one that carries no
/// principles file in the same scope.
/// </summary>
internal sealed class ScriptedSourceProviderFactory(
    IReadOnlyDictionary<string, ScriptedSourceProvider> byRepo) : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config) =>
        byRepo.TryGetValue(config.Name, out var provider)
            ? provider
            : throw new InvalidOperationException($"No scripted provider for repo '{config.Name}'.");
}
