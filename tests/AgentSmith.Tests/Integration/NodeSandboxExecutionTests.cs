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
        // .npmrc the way the agent would produce it post-p0191: registry +
        // _authToken on the host. The exact path that NU1301 produced for
        // .NET, but for npm.
        var registryHost = new Uri(registry).Host;
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, ".npmrc"),
            $"""
            registry={registry}
            //{registryHost}/:_authToken={token}
            always-auth=true
            """);
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
