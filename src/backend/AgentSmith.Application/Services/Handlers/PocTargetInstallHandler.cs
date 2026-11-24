using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Argo;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933dd: the step of a poc-target-install run — renders the build pipeline for the host of the target's
/// source repo and puts it on the default branch in one checkout-free write (<see cref="PocPipelineWrite"/>). The c81e
/// series extends this step with the app template and the ApplicationSet.
/// <para>2026-11-23-c81eb: then writes the default app template's absent files (<see cref="PocAppTemplateWrite"/>).</para>
/// <para>2026-11-23-c81ec: then upserts the target's ApplicationSet on its Argo CD, or says it is unchanged
/// (<see cref="PocApplicationSetInstall"/>).</para>
/// </summary>
public sealed class PocTargetInstallHandler(
    PocsConfigSource config, PocSourceRepos repos, PocPipelineTemplate template, PocPipelineWrite write, PocAppTemplateWrite appTemplate,
    PocApplicationSetInstall applicationSet)
    : ICommandHandler<PocTargetInstallContext>
{
    public async Task<CommandResult> ExecuteAsync(PocTargetInstallContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var all = config.All();
        if (all.Pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, context.Target, StringComparison.OrdinalIgnoreCase)) is not { } target)
            return CommandResult.Fail($"A poc-target-install run needs a configured PoC target; '{context.Target}' is none of pocs.targets.");
        var (repo, unresolved) = repos.Resolve(all, target);
        if (repo is null) return CommandResult.Fail(unresolved!);
        var (file, refused) = template.Render(repo.Type, PocPipelineValues.Of(target, all.Pocs.Scan));
        if (file is null) return CommandResult.Fail($"The build pipeline of target '{target.Name}' was not written: {refused}.");
        var pipeline = await write.WriteAsync(repo, target, file, cancellationToken);
        if (!pipeline.IsSuccess) return pipeline;
        var (written, said) = await appTemplate.WriteAsync(repo, target, all.Pocs.Gitops.MaxFolderFiles, cancellationToken);
        if (!written) return CommandResult.Fail($"{pipeline.Message} {said}");
        var (installed, set) = await applicationSet.InstallAsync(repo, target, cancellationToken);
        return installed ? CommandResult.Ok($"{pipeline.Message} {said} {set}") : CommandResult.Fail($"{pipeline.Message} {said} {set}");
    }
}
