using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds sandbox-side git Steps for the checkout flow, parameterised by the
/// target directory inside the sandbox. Extracted from CheckoutSourceHandler
/// to keep both classes under the 120-LOC limit after multi-repo iteration
/// was introduced. Pure factory: no state, no I/O.
/// </summary>
internal static class CheckoutStepFactory
{
    private const int CloneTimeoutSeconds = 300;
    private const int CheckoutTimeoutSeconds = 60;

    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    public static Step BuildMkdirStep(string targetDir) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "mkdir",
            Args: new[] { "-p", targetDir },
            WorkingDirectory: "/",
            TimeoutSeconds: CheckoutTimeoutSeconds);

    public static Step BuildCloneStep(RepoConnection config, string targetDir)
    {
        var token = GitTokenResolver.Resolve(config.Type);
        var env = token is null
            ? null
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["GIT_TOKEN"] = token };

        return new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "-c", CredHelper, "clone", config.Url!, "." },
            WorkingDirectory: targetDir,
            Env: env,
            TimeoutSeconds: CloneTimeoutSeconds);
    }

    public static Step BuildCheckoutStep(string branch, string workingDirectory) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "checkout", branch },
            WorkingDirectory: workingDirectory,
            TimeoutSeconds: CheckoutTimeoutSeconds);

    public static Step BuildCreateBranchStep(string branch, string workingDirectory) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "checkout", "-b", branch },
            WorkingDirectory: workingDirectory,
            TimeoutSeconds: CheckoutTimeoutSeconds);
}
