using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283dg: reads the root DESIGN.md of every repository in the run out of the
/// sandboxes that serve it and publishes one document per repository that carries one at
/// ContextKeys.DesignSystem. The file sits at the repository root, so a repository served by
/// several sandboxes (one per context or toolchain) holds ONE file: the first sandbox that
/// answers speaks for it. An absent file is the common case and publishes nothing.
/// </summary>
public sealed class LoadDesignSystemHandler(
    ISandboxFileReaderFactory readerFactory,
    SandboxTargets sandboxTargets,
    ILogger<LoadDesignSystemHandler> logger)
    : ICommandHandler<LoadDesignSystemContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadDesignSystemContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) || repos is null)
            return CommandResult.Ok("No repositories in pipeline context, skipping");

        var loaded = new List<ContextDocument>();
        foreach (var repo in repos)
        {
            var document = await ReadAsync(pipeline, repo, cancellationToken);
            if (document is not null) loaded.Add(document);
        }

        if (loaded.Count > 0)
            pipeline.Set<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem, loaded);
        return CommandResult.Ok(
            $"Loaded {ProjectMetaPaths.DesignSystem} from {loaded.Count} of {repos.Count} repo(s)");
    }

    private async Task<ContextDocument?> ReadAsync(
        PipelineContext pipeline, RepoConnection repo, CancellationToken ct)
    {
        var path = Path.Combine(Repository.SandboxWorkPath, ProjectMetaPaths.DesignSystem);
        foreach (var (key, sandbox) in sandboxTargets.SandboxesForRepo(pipeline, repo))
        {
            var content = await readerFactory.Create(sandbox).TryReadAsync(path, ct);
            if (string.IsNullOrWhiteSpace(content)) continue;
            logger.LogInformation("{Key}: loaded {Path} ({Chars} chars)", key, path, content.Length);
            return new ContextDocument(repo.Name, null, null, ProjectMetaPaths.DesignSystem, content);
        }
        return null;
    }
}
