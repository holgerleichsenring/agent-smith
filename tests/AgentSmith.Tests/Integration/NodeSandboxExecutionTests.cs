using System.Text;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0197: real `npm install` via InProcessSandbox.RunStepAsync. Skip when
/// npm isn't on PATH (CI runners that only have dotnet). Private-registry
/// test is env-var gated — operator sets a token and the test exercises
/// the full .npmrc-token-injection flow that the agent uses in production.
/// </summary>
public sealed class NodeSandboxExecutionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task NpmInstall_TrivialPackageJson_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("npm")) return;

        await using var fixture = FixtureWorkdir.CreatePackageJson(
            "smoke",
            // is-odd@3 is 2 lines, no transitive deps, no native build — fastest
            // install we can do that still proves the registry path works.
            """ "is-odd": "3.0.1" """);
        await using var sandbox = NewSandbox(fixture.Path);

        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "npm", new[] { "install", "--no-audit", "--no-fund", "--loglevel=error" }, timeoutSec: 180),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0, "npm install of a public package must succeed");
        File.Exists(Path.Combine(fixture.Path, "node_modules", "is-odd", "package.json"))
            .Should().BeTrue("node_modules should contain the resolved dependency");
    }

    [Fact]
    public async Task NpmInstall_PrivateRegistryWithToken_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("npm")) return;
        var registry = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_NPM_REGISTRY");
        var token = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_NPM_TOKEN");
        var packageName = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_NPM_PACKAGE");
        if (string.IsNullOrWhiteSpace(registry) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(packageName))
        {
            output.WriteLine("Skipped: AGENTSMITH_TEST_NPM_REGISTRY + AGENTSMITH_TEST_NPM_TOKEN + AGENTSMITH_TEST_NPM_PACKAGE not set");
            return;
        }

        await using var fixture = FixtureWorkdir.CreatePackageJson(
            "private-smoke",
            $""" "{packageName}": "*" """);
        // .npmrc the way the agent would produce it post-p0191:
        //   * scope-targeted registry so non-scoped deps still go to npmjs
        //   * _authToken keyed on the full registry PATH (Azure Artifacts
        //     ignores host-only auth lines)
        //   * always-auth=true so the token is sent on every request
        var rxUri = new Uri(registry);
        var registryNoScheme = registry.Substring(registry.IndexOf("//", StringComparison.Ordinal));
        if (!registryNoScheme.EndsWith('/')) registryNoScheme += '/';
        var npmrc = new StringBuilder();
        if (packageName.StartsWith('@'))
        {
            var scope = packageName[..packageName.IndexOf('/')];
            npmrc.AppendLine($"{scope}:registry={registry}");
        }
        else
        {
            npmrc.AppendLine($"registry={registry}");
        }
        npmrc.AppendLine($"{registryNoScheme}:_authToken={token}");
        npmrc.AppendLine("always-auth=true");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, ".npmrc"), npmrc.ToString());
        await using var sandbox = NewSandbox(fixture.Path);

        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "npm", new[] { "install", "--no-audit", "--no-fund", "--loglevel=error" }, timeoutSec: 180),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0,
            "private npm registry install with valid PAT must succeed — exact path tested for the EAUTH bug class");
    }

    private static InProcessSandbox NewSandbox(string workdir) =>
        new(jobId: "test", workDir: workdir, ownsWorkDir: false, NullLogger.Instance);

    private static Step BuildRunStep(string cwd, string cmd, string[] args, int timeoutSec) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd, Args: args, WorkingDirectory: cwd, TimeoutSeconds: timeoutSec);
}
