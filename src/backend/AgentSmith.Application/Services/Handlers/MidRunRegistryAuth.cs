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
public sealed class MidRunRegistryAuth(SetupRegistryAuthHandler handler)
{
    public async Task<IReadOnlyList<string>> StageAsync(
        PipelineContext pipeline, string repoKey, ISandbox sandbox, CancellationToken cancellationToken, bool generic = true)
    {
        var registries = pipeline.ProjectRegistries(); // 2026-11-25-9992d: the run's project's feeds
        if (registries.Count == 0) return [];
        var written = new List<string>();
        await handler.StageInSandboxAsync(repoKey, sandbox, handler.RootsOf(pipeline, sandbox), registries,
            () => pipeline.Resolved().Agent, written, cancellationToken, generic);
        var staged = pipeline.TryGet<List<string>>(ContextKeys.StagedRegistries, out var held) && held is not null
            ? new List<string>(held) : [];
        staged.AddRange(written.Select(w => $"{repoKey}: {w}").Where(line => !staged.Contains(line)).ToList()); // 9992b: a restart re-stages
        pipeline.Set(ContextKeys.StagedRegistries, staged);
        return written;
    }
}
