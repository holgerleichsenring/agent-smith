using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: source provider backed by a per-test local bare repo plus a
/// seeded working copy. ProviderType is GitHub (not Local) so
/// CheckoutSourceHandler runs the real <c>git clone</c> path inside the
/// sandbox against the bind-mounted bare-repo URL. Pre-sandbox file/dir
/// reads (used by SandboxLanguageResolver) read straight from the
/// working-copy temp dir, so the language is discovered before the clone.
/// </summary>
internal sealed class LocalGitSourceProvider(DockerHarnessSession session) : ISourceProvider
{
    public string ProviderType => "GitHub";

    public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ConnectionProbeResult.Reachable(0));

    public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken) =>
        Task.FromResult(new Repository(
            branch ?? new BranchName("main"), session.InSandboxBareUrl));

    public Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken, TicketId? linkedTicketId = null) =>
        Task.FromResult($"https://fake.local/pulls/{linkedTicketId?.Value ?? "1"}");

    public Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var full = Path.Combine(session.WorkingCopyPath, path);
        return Task.FromResult(File.Exists(full) ? File.ReadAllText(full) : null);
    }

    public Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        var full = Path.Combine(session.WorkingCopyPath, path);
        if (!Directory.Exists(full))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        var names = Directory.EnumerateFileSystemEntries(full)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(names!);
    }

    public Task<bool> UpdatePullRequestBodyAsync(string prUrl, string newBody, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
