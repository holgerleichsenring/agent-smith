using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0196: ISourceProvider stub. CheckoutAsync returns a canned Repository;
/// TryReadFileAsync serves a synthetic .agentsmith/contexts/default/context.yaml
/// so SandboxLanguageResolver picks a known language; CreatePullRequestAsync
/// returns a fake URL. No real git, no network.
/// </summary>
internal sealed class StubSourceProvider : ISourceProvider
{
    // "Local" so CheckoutSourceHandler short-circuits the git-clone path:
    // local repos trust the sandbox bind-mount and skip the in-sandbox clone.
    public string ProviderType => "Local";

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Reachable(0));

    public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
        Task.FromResult(new Repository(
            branch ?? new BranchName("agent-smith/stub"), "git://stub"));

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken, TicketId? linkedTicketId = null, bool isDraft = false) =>
        Task.FromResult($"https://stub.test/pulls/{linkedTicketId?.Value ?? "1"}");

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (path.EndsWith("context.yaml", StringComparison.Ordinal))
        {
            // prerequisites exercises the p0202/p0202a install wiring
            // end-to-end (context.yaml -> SandboxLanguageResolver -> discovery
            // -> EnsurePrerequisitesHandler -> sandbox run). The handler is
            // language-agnostic, so the csharp/npm pairing is irrelevant — the
            // point is to prove the durable value actually reaches the sandbox
            // (the p0202 no-op slipped through precisely because no harness
            // context.yaml carried an prerequisites).
            return Task.FromResult<string?>("""
                meta:
                  workdir: .
                  project: stub
                stack:
                  lang: csharp
                prerequisites: "npm ci"
                """);
        }
        return Task.FromResult<string?>(null);
    }

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (path == ".agentsmith/contexts")
            return Task.FromResult<IReadOnlyList<string>>(new[] { "default" });
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}

internal sealed class StubSourceProviderFactory : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config) => new StubSourceProvider();
}
