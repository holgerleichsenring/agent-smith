namespace AgentSmith.Application.Services.Demo;

/// <summary>
/// p0326: turns a freshly materialized demo workspace into a local git repo
/// with one initial commit, so the fix-bug run's result is a reviewable
/// `git diff HEAD~1` against the seeded-bug baseline.
/// </summary>
public interface IDemoGitInitializer
{
    Task InitializeAsync(string workspaceDir, CancellationToken cancellationToken);
}
