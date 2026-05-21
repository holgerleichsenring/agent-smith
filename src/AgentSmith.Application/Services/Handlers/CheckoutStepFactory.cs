using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds sandbox-side git Steps for the checkout flow. Each step runs inside
/// the per-repo sandbox where /work is the repo root (p0158e), so the workdir
/// is always /work — no per-call target directory parameter.
/// </summary>
internal static class CheckoutStepFactory
{
    private const int CloneTimeoutSeconds = 300;
    private const int CheckoutTimeoutSeconds = 60;

    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    public static Step BuildCloneStep(RepoConnection config)
    {
        var token = GitTokenResolver.Resolve(config.Type);
        var env = token is null
            ? null
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["GIT_TOKEN"] = token };

        return new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "-c", CredHelper, "clone", config.Url!, "." },
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: env,
            TimeoutSeconds: CloneTimeoutSeconds);
    }

    public static Step BuildCheckoutStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "checkout", branch },
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);

    public static Step BuildCreateBranchStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "checkout", "-b", branch },
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);
}
