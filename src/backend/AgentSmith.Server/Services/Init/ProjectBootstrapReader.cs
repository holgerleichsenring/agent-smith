using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Init;

/// <summary>
/// 2026-10-13-0b82c: whether each repository of a project is bootstrapped, read from its host when a
/// person asks — nothing is stored, so there is no state to go stale. A configured default branch is
/// read by name; otherwise the host's own default is read and reported as such.
/// </summary>
public sealed class ProjectBootstrapReader(
    IConfigurationLoader configLoader, ServerContext serverContext, RemoteBootstrapProbe probe)
{
    public async Task<ProjectBootstrapLookup> ReadAsync(string projectName, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project))
            return ProjectBootstrapLookup.UnknownProject;
        var entries = new List<ProjectBootstrapEntry>(project.Repos.Count);
        foreach (var repo in project.Repos) entries.Add(await ReadOneAsync(repo, ct));
        return new ProjectBootstrapLookup(true, entries);
    }

    private async Task<ProjectBootstrapEntry> ReadOneAsync(RepoConnection repo, CancellationToken ct)
    {
        var branch = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? null : new BranchName(repo.DefaultBranch);
        var verdict = await probe.ProbeAsync(repo, branch, ct);
        return new ProjectBootstrapEntry(repo.Name, branch?.Value ?? ProjectBootstrapEntry.DefaultBranch,
            verdict.State.ToString().ToLowerInvariant(), [.. verdict.Missing.Select(m => m.Describe())], verdict.Reason);
    }
}
