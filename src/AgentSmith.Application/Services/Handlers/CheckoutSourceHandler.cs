using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Checks out a source repository. Pure-Step after p0117b: provider.CheckoutAsync
/// returns metadata only (default branch, clone URL); the actual git clone runs
/// in the sandbox via Step{Kind=Run}, plus an optional `git checkout &lt;branch&gt;`
/// when the requested branch differs from the resolved default.
/// </summary>
public sealed class CheckoutSourceHandler(
    ISourceProviderFactory factory,
    ILogger<CheckoutSourceHandler> logger)
    : ICommandHandler<CheckoutSourceContext>
{
    private const int CloneTimeoutSeconds = 300;
    private const int CheckoutTimeoutSeconds = 60;

    public async Task<CommandResult> ExecuteAsync(
        CheckoutSourceContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Resolving source metadata for branch {Branch}...", context.Branch);

        var provider = factory.Create(context.Config);
        var repo = await provider.CheckoutAsync(context.Branch, cancellationToken);
        context.Pipeline.Set(ContextKeys.Repository, repo);

        if (provider.ProviderType.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            // Local source: operator binds basePath to the sandbox /work outside this code.
            // No clone required; trust the bind-mount.
            logger.LogInformation("Local source provider — sandbox /work assumed bind-mounted");
            return CommandResult.Ok($"Local source ready at {Repository.SandboxWorkPath}");
        }

        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return CommandResult.Fail("CheckoutSource requires an active sandbox to perform the clone.");

        if (string.IsNullOrEmpty(context.Config.Url))
            return CommandResult.Fail("CheckoutSource requires a non-empty source URL for non-local providers.");

        var cloneResult = await RunStepAsync(sandbox, BuildCloneStep(context.Config.Url), cancellationToken);
        if (cloneResult.ExitCode != 0)
            return CommandResult.Fail($"git clone failed (exit={cloneResult.ExitCode}): {cloneResult.ErrorMessage}");
        logger.LogInformation("Sandbox-side `git clone` completed in {Duration:F1}s", cloneResult.DurationSeconds);

        if (NeedsBranchSwitch(context.Branch, repo.CurrentBranch))
        {
            var branchValue = context.Branch?.Value ?? repo.CurrentBranch.Value;
            var checkoutResult = await RunStepAsync(sandbox, BuildCheckoutStep(branchValue), cancellationToken);
            if (checkoutResult.ExitCode != 0)
                logger.LogWarning(
                    "Sandbox-side `git checkout {Branch}` failed (exit={Exit}): {Error}",
                    branchValue, checkoutResult.ExitCode, checkoutResult.ErrorMessage);
        }

        return CommandResult.Ok($"Repository cloned to {Repository.SandboxWorkPath}");
    }

    private static bool NeedsBranchSwitch(BranchName? requested, BranchName resolvedDefault) =>
        requested is not null
        && !requested.Value.Equals(resolvedDefault.Value, StringComparison.Ordinal);

    private static Task<StepResult> RunStepAsync(ISandbox sandbox, Step step, CancellationToken ct) =>
        sandbox.RunStepAsync(step, progress: null, ct);

    private static Step BuildCloneStep(string repoUrl)
    {
        const string credHelper = "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";
        return new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", credHelper, "clone", repoUrl, "."],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CloneTimeoutSeconds);
    }

    private static Step BuildCheckoutStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["checkout", branch],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);
}
