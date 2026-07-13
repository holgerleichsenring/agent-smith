using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Checks out the run's repos. Iterates Configs: each repo is cloned into
/// its OWN per-repo sandbox at /work (the per-repo sandbox is created by
/// PipelineSandboxCoordinator before this handler runs). Local providers
/// trust the sandbox bind-mount and skip the clone. The primary Repository
/// (Configs[0]) is published on ContextKeys.Repository for downstream skills
/// that read a singular repo; multi-repo skills consume ContextKeys.Repos +
/// ContextKeys.Sandboxes directly.
/// p0331: the per-repo clone/branch-switch mechanics moved to
/// <see cref="SandboxRepoCloner"/> so the ensure_repo_sandbox mid-run
/// escalation reuses the identical checkout path.
/// </summary>
public sealed class CheckoutSourceHandler(
    SandboxRepoCloner cloner,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<CheckoutSourceHandler> logger)
    : ICommandHandler<CheckoutSourceContext>, IConceptWriter
{
    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        new[] { new ConceptDeclaration("source_available", ConceptType.Bool) };

    public async Task<CommandResult> ExecuteAsync(
        CheckoutSourceContext context, CancellationToken cancellationToken)
    {
        var result = await CheckoutAllAsync(context, cancellationToken);
        conceptsFactory(context.Pipeline).SetBool("source_available", result.IsSuccess);
        return result;
    }

    private async Task<CommandResult> CheckoutAllAsync(
        CheckoutSourceContext context, CancellationToken ct)
    {
        if (context.Configs.Count == 0)
            return CommandResult.Fail("No repos configured; nothing to check out.");

        Repository? primary = null;
        for (var i = 0; i < context.Configs.Count; i++)
        {
            var config = context.Configs[i];
            logger.LogInformation("Checking out {Repo} into its sandbox(es) at /work...", config.Name);
            var repo = await cloner.CheckoutIntoSandboxesAsync(
                config, context.Branch,
                SandboxTargets.SandboxesForRepo(context.Pipeline, config), ct);
            if (repo is null)
                return CommandResult.Fail($"Checkout failed for repo '{config.Name}'.");
            if (i == 0) primary = repo;
        }
        context.Pipeline.Set(ContextKeys.Repository, primary!);
        return CommandResult.Ok($"Checked out {context.Configs.Count} repo(s)");
    }
}
