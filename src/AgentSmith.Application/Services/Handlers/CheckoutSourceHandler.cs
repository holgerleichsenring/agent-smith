using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Checks out a source repository. V1 hybrid: LibGit2Sharp on the Server-Pod
/// keeps Repository.LocalPath populated for downstream branch/commit/PR handlers.
/// When a sandbox is active, additionally pushes a `git clone` Step so /work in
/// the sandbox is populated for AgenticExecute / Test. Branch/commit/push
/// migration to Step-based git is deferred to a follow-up phase.
/// </summary>
public sealed class CheckoutSourceHandler(
    ISourceProviderFactory factory,
    ILogger<CheckoutSourceHandler> logger)
    : ICommandHandler<CheckoutSourceContext>
{
    private const int CloneTimeoutSeconds = 300;

    public async Task<CommandResult> ExecuteAsync(
        CheckoutSourceContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking out branch {Branch}...", context.Branch);

        var provider = factory.Create(context.Config);
        var repo = await provider.CheckoutAsync(context.Branch, cancellationToken);
        context.Pipeline.Set(ContextKeys.Repository, repo);

        await TryCloneIntoSandboxAsync(context, cancellationToken);

        return CommandResult.Ok($"Repository checked out to {repo.LocalPath}");
    }

    private async Task TryCloneIntoSandboxAsync(
        CheckoutSourceContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return;
        if (string.IsNullOrEmpty(context.Config.Url))
        {
            logger.LogInformation("Skipping sandbox-side clone (Source.Url not set)");
            return;
        }

        var step = BuildCloneStep(context.Config.Url);
        var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
        if (result.ExitCode != 0)
            logger.LogWarning("Sandbox-side `git clone` failed (exit={Exit}): {Error}",
                result.ExitCode, result.ErrorMessage);
        else
            logger.LogInformation("Sandbox-side `git clone` completed in {Duration:F1}s",
                result.DurationSeconds);
    }

    private static Step BuildCloneStep(string repoUrl)
    {
        const string credHelper = "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";
        return new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", credHelper, "clone", repoUrl, "."],
            TimeoutSeconds: CloneTimeoutSeconds);
    }
}
