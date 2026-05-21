using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
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
/// </summary>
public sealed class CheckoutSourceHandler(
    ISourceProviderFactory factory,
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
            var repo = await CheckoutOneAsync(context, context.Configs[i], ct);
            if (repo is null)
                return CommandResult.Fail($"Checkout failed for repo '{context.Configs[i].Name}'.");
            if (i == 0) primary = repo;
        }
        context.Pipeline.Set(ContextKeys.Repository, primary!);
        return CommandResult.Ok($"Checked out {context.Configs.Count} repo(s)");
    }

    private async Task<Repository?> CheckoutOneAsync(
        CheckoutSourceContext context, RepoConnection config, CancellationToken ct)
    {
        logger.LogInformation("Checking out {Repo} into its sandbox at /work...", config.Name);

        var provider = factory.Create(config);
        var resolved = await provider.CheckoutAsync(context.Branch, ct);
        var repo = new Repository(resolved.CurrentBranch, resolved.RemoteUrl);

        if (provider.ProviderType.Equals("Local", StringComparison.OrdinalIgnoreCase))
            return repo;

        if (!context.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null
            || !sandboxes.TryGetValue(config.Name, out var sandbox))
            return FailWith($"No sandbox for repo '{config.Name}'.", config);
        if (string.IsNullOrEmpty(config.Url))
            return FailWith("CheckoutSource requires a non-empty source URL for non-local providers.", config);

        var clone = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCloneStep(config), null, ct);
        if (clone.ExitCode != 0)
            return FailWith($"git clone failed (exit={clone.ExitCode}): {clone.ErrorMessage}", config);

        await MaybeSwitchBranchAsync(sandbox, context.Branch, resolved.CurrentBranch, ct);
        return repo;
    }

    private Repository? FailWith(string message, RepoConnection config)
    {
        logger.LogWarning("{Repo}: {Message}", config.Name, message);
        return null;
    }

    private async Task MaybeSwitchBranchAsync(
        ISandbox sandbox, BranchName? requested, BranchName resolvedDefault, CancellationToken ct)
    {
        if (requested is null
            || requested.Value.Equals(resolvedDefault.Value, StringComparison.Ordinal))
            return;
        var branch = requested.Value;

        var existing = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(branch), null, ct);
        if (existing.ExitCode == 0)
        {
            logger.LogInformation("git checkout {Branch} (existing)", branch);
            return;
        }
        var created = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCreateBranchStep(branch), null, ct);
        if (created.ExitCode != 0)
            logger.LogWarning(
                "git checkout -b {Branch} failed (exit={Exit}): {Err}",
                branch, created.ExitCode, created.ErrorMessage);
    }
}
