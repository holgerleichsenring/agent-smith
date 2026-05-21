using System.Text;
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
/// Loads each repo's coding-principles.md (p0158f). Iterates ContextKeys.Repos,
/// reads each repo's `.agentsmith/coding-principles.md` from its per-repo
/// sandbox. Populates ContextKeys.RepoCodingPrinciples (Dictionary&lt;repoName,
/// content&gt;) for multi-repo-aware consumers AND legacy ContextKeys.DomainRules
/// as a single concatenated string with per-repo `## {repo}` headers so
/// AgenticExecute sees all principles inline in one document. Missing files are
/// optional — repos without principles are simply omitted.
/// </summary>
public sealed class LoadCodingPrinciplesHandler(
    IProjectMetaResolver metaResolver,
    ISandboxFileReaderFactory readerFactory,
    ILogger<LoadCodingPrinciplesHandler> logger)
    : ICommandHandler<LoadCodingPrinciplesContext>
{
    private const string DefaultRelativePath = ProjectMetaPaths.CodingPrinciples;

    public async Task<CommandResult> ExecuteAsync(
        LoadCodingPrinciplesContext context, CancellationToken cancellationToken)
    {
        var (sandboxes, repos) = MultiRepoTargets.Resolve(context.Pipeline);
        if (sandboxes is null || repos is null)
            return CommandResult.Ok("No Sandboxes / Repos / legacy Sandbox in pipeline context, skipping");

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repo in repos)
        {
            if (!sandboxes.TryGetValue(repo.Name, out var sandbox)) continue;
            var content = await TryReadOneAsync(context, sandbox, repo.Name, cancellationToken);
            if (content is not null) loaded[repo.Name] = content;
        }

        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.RepoCodingPrinciples, loaded);
        if (loaded.Count > 0)
            context.Pipeline.Set(ContextKeys.DomainRules, Aggregate(loaded));

        return CommandResult.Ok($"Loaded {loaded.Count} of {repos.Count} repo coding principles");
    }

    private async Task<string?> TryReadOneAsync(
        LoadCodingPrinciplesContext context, ISandbox sandbox, string repoName, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var direct = Path.Combine(Repository.SandboxWorkPath, context.RelativePath);
        if (await reader.ExistsAsync(direct, ct))
            return await reader.ReadRequiredAsync(direct, ct);

        if (!string.Equals(context.RelativePath, DefaultRelativePath, StringComparison.OrdinalIgnoreCase))
            return null;

        var metaDir = await metaResolver.ResolveAsync(reader, Repository.SandboxWorkPath, ct);
        if (metaDir is null)
        {
            logger.LogInformation("{Repo}: no .agentsmith/ found, no principles loaded", repoName);
            return null;
        }
        var nested = Path.Combine(metaDir, "coding-principles.md");
        if (!await reader.ExistsAsync(nested, ct)) return null;
        var content = await reader.ReadRequiredAsync(nested, ct);
        logger.LogInformation("{Repo}: loaded principles from {Path} ({Chars} chars)", repoName, nested, content.Length);
        return content;
    }

    private static string Aggregate(IReadOnlyDictionary<string, string> perRepo)
    {
        // Single-repo back-compat: output content verbatim (no header / separator).
        if (perRepo.Count == 1) return perRepo.Values.First();
        var sb = new StringBuilder();
        var first = true;
        foreach (var (repoName, content) in perRepo)
        {
            if (!first) sb.Append("\n\n---\n\n");
            sb.Append($"## {repoName}\n\n");
            sb.Append(content.TrimEnd());
            first = false;
        }
        return sb.ToString();
    }
}
