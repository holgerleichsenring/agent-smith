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
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<CheckoutSourceHandler> logger)
    : ICommandHandler<CheckoutSourceContext>, IConceptWriter
{
    private const int CloneTimeoutSeconds = 300;
    private const int CheckoutTimeoutSeconds = 60;

    public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; } =
        [new ConceptDeclaration("source_available", ConceptType.Bool)];

    public async Task<CommandResult> ExecuteAsync(
        CheckoutSourceContext context, CancellationToken cancellationToken)
    {
        var result = await RunCheckoutAsync(context, cancellationToken);
        conceptsFactory(context.Pipeline).SetBool("source_available", result.IsSuccess);
        return result;
    }

    private async Task<CommandResult> RunCheckoutAsync(
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

        return await CloneAndSwitchAsync(context, repo, cancellationToken);
    }

    private async Task<CommandResult> CloneAndSwitchAsync(
        CheckoutSourceContext context, Repository repo, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return CommandResult.Fail("CheckoutSource requires an active sandbox to perform the clone.");

        if (string.IsNullOrEmpty(context.Config.Url))
            return CommandResult.Fail("CheckoutSource requires a non-empty source URL for non-local providers.");

        var cloneResult = await RunStepAsync(sandbox, BuildCloneStep(context.Config), cancellationToken);
        if (cloneResult.ExitCode != 0)
            return CommandResult.Fail($"git clone failed (exit={cloneResult.ExitCode}): {cloneResult.ErrorMessage}");
        logger.LogInformation("Sandbox-side `git clone` completed in {Duration:F1}s", cloneResult.DurationSeconds);

        await MaybeSwitchBranchAsync(sandbox, context.Branch, repo.CurrentBranch, cancellationToken);
        return CommandResult.Ok($"Repository cloned to {Repository.SandboxWorkPath}");
    }

    private async Task MaybeSwitchBranchAsync(
        ISandbox sandbox, BranchName? requested, BranchName resolvedDefault, CancellationToken cancellationToken)
    {
        if (!NeedsBranchSwitch(requested, resolvedDefault)) return;
        var branchValue = requested?.Value ?? resolvedDefault.Value;

        // Two-step: try to check out an existing remote-tracking branch first
        // (a re-run on the same ticket lands here when the previous run already
        // pushed agent-smith/<id>). If that fails, fall through to `checkout -b`
        // to create a fresh local branch off the default. Without the fallback
        // we'd stay on the default branch and InitCommit's push would either
        // accidentally target the default branch or fail force-with-lease.
        var checkoutResult = await RunStepAsync(sandbox, BuildCheckoutStep(branchValue), cancellationToken);
        if (checkoutResult.ExitCode == 0)
        {
            logger.LogInformation(
                "Sandbox-side `git checkout {Branch}` succeeded (reusing existing branch)",
                branchValue);
            return;
        }

        logger.LogInformation(
            "Sandbox-side `git checkout {Branch}` did not find the branch ({Error}); creating it locally",
            branchValue, checkoutResult.ErrorMessage ?? "no error message");

        var createResult = await RunStepAsync(sandbox, BuildCreateBranchStep(branchValue), cancellationToken);
        if (createResult.ExitCode != 0)
            logger.LogWarning(
                "Sandbox-side `git checkout -b {Branch}` failed (exit={Exit}): {Error}",
                branchValue, createResult.ExitCode, createResult.ErrorMessage);
        else
            logger.LogInformation(
                "Sandbox-side `git checkout -b {Branch}` created new branch off default",
                branchValue);
    }

    private static bool NeedsBranchSwitch(BranchName? requested, BranchName resolvedDefault) =>
        requested is not null
        && !requested.Value.Equals(resolvedDefault.Value, StringComparison.Ordinal);

    private static Task<StepResult> RunStepAsync(ISandbox sandbox, Step step, CancellationToken ct) =>
        sandbox.RunStepAsync(step, progress: null, ct);

    private static Step BuildCloneStep(RepoConnection config)
    {
        const string credHelper = "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

        // Server (K8s) mode injects GIT_TOKEN at pod creation via PodSpecBuilder
        // sourcing from a Secret. CLI / InProcessSandbox mode reads the per-platform
        // host env var (AZURE_DEVOPS_TOKEN / GITHUB_TOKEN / GITLAB_TOKEN) and stamps
        // it as GIT_TOKEN on the Step so the credential helper has something to
        // echo. Without this CLI-mode clones fail with exit 128 against private
        // remotes.
        var token = GitTokenResolver.Resolve(config.Type);
        var env = token is null
            ? null
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["GIT_TOKEN"] = token };

        return new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", credHelper, "clone", config.Url!, "."],
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: env,
            TimeoutSeconds: CloneTimeoutSeconds);
    }

    private static Step BuildCheckoutStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["checkout", branch],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);

    private static Step BuildCreateBranchStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["checkout", "-b", branch],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);
}
