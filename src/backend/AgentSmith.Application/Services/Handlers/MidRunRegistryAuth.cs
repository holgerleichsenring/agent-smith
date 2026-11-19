using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-18-9c1cd: stages private-feed credentials in ONE sandbox a run gained mid-way (ensure_repo_sandbox), as
/// <see cref="SetupRegistryAuthHandler"/> staged them in the sandboxes the run started with, and adds what it wrote
/// to the run's staged list — so no master needs a tool that hands it credentials.
/// </summary>
public sealed class MidRunRegistryAuth(SetupRegistryAuthHandler handler, AgentSmithConfig config)
{
    public async Task<IReadOnlyList<string>> StageAsync(
        PipelineContext pipeline, string repoKey, ISandbox sandbox, CancellationToken cancellationToken)
    {
        if (config.Registries.Count == 0) return [];
        var written = new List<string>();
        await handler.StageInSandboxAsync(repoKey, sandbox, () => pipeline.Resolved().Agent, written, cancellationToken);
        var staged = pipeline.TryGet<List<string>>(ContextKeys.StagedRegistries, out var held) && held is not null
            ? new List<string>(held) : [];
        staged.AddRange(written.Select(w => $"{repoKey}: {w}"));
        pipeline.Set(ContextKeys.StagedRegistries, staged);
        return written;
    }
}
