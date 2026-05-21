using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads each repo's context.yaml from its per-repo sandbox (.agentsmith/ at the
/// sandbox /work root, p0158e+f). Iterates ContextKeys.Repos; populates
/// ContextKeys.RepoContextYamls (Dictionary&lt;repoName, yamlContent&gt;) and
/// legacy ContextKeys.ProjectContext (= primary repo's YAML) for back-compat.
/// Missing files are not an error — coding-principles+context are optional.
/// </summary>
public sealed class LoadContextHandler(
    IProjectMetaResolver metaResolver,
    ISandboxFileReaderFactory readerFactory,
    ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    private const string FileName = "context.yaml";

    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken cancellationToken)
    {
        var (sandboxes, repos) = MultiRepoTargets.Resolve(context.Pipeline);
        if (sandboxes is null || repos is null)
            return CommandResult.Ok("No Sandboxes / Repos / legacy Sandbox in pipeline context, skipping");

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repo in repos)
        {
            if (!sandboxes.TryGetValue(repo.Name, out var sandbox)) continue;
            var content = await TryReadOneAsync(sandbox, repo.Name, cancellationToken);
            if (content is not null) loaded[repo.Name] = content;
        }

        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.RepoContextYamls, loaded);
        if (loaded.TryGetValue(repos[0].Name, out var primary))
            context.Pipeline.Set(ContextKeys.ProjectContext, primary);

        if (loaded.Count == 0)
            return CommandResult.Ok("No project context loaded");
        if (loaded.Count == 1)
            return CommandResult.Ok($"Loaded project context ({loaded.Values.First().Length} chars)");
        return CommandResult.Ok($"Loaded {loaded.Count} of {repos.Count} repo context(s)");
    }

    private async Task<string?> TryReadOneAsync(
        ISandbox sandbox, string repoName, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var metaDir = await metaResolver.ResolveAsync(reader, Repository.SandboxWorkPath, ct);
        if (metaDir is null)
        {
            logger.LogInformation("{Repo}: no .agentsmith/ found under /work, skipping", repoName);
            return null;
        }
        var path = Path.Combine(metaDir, FileName);
        var content = await reader.TryReadAsync(path, ct);
        if (content is null)
        {
            logger.LogInformation("{Repo}: no {File} in {Dir}, skipping", repoName, FileName, metaDir);
            return null;
        }
        logger.LogInformation("{Repo}: loaded {Path} ({Chars} chars)", repoName, path, content.Length);
        return content;
    }
}
