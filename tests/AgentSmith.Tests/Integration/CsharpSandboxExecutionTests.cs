using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0197: real Process.Start integration via InProcessSandbox. Exercises
/// `dotnet build`, `dotnet test`, `dotnet restore` against actual fixture
/// projects in temp dirs. Catches the class of bug where "the test suite
/// is green but operator's pipeline fails 5 minutes in" because the test
/// suite never exercised the actual toolchain command.
///
/// Skip-pattern: each test guards on dotnet being on PATH (it is on
/// every agent-smith CI runner; locally it's whatever the dev has).
/// </summary>
public sealed class CsharpSandboxExecutionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task DotnetBuild_TrivialConsoleProject_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("dotnet")) return;

        await using var fixture = await FixtureWorkdir.CreateConsoleProjectAsync("Smoke");
        await using var sandbox = NewSandbox(fixture.Path);

        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "dotnet", new[] { "build", "--nologo", "-v", "minimal" }, timeoutSec: 120),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0,
            "dotnet build on a trivial console project must succeed; operator's run-time bug class otherwise");
    }

    [Fact]
    public async Task DotnetRestore_PublicNugetOrg_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("dotnet")) return;

        await using var fixture = await FixtureWorkdir.CreateConsoleProjectAsync("RestoreSmoke", extraPackage: "Newtonsoft.Json");
        await using var sandbox = NewSandbox(fixture.Path);

        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "dotnet", new[] { "restore", "--nologo", "-v", "minimal" }, timeoutSec: 180),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0,
            "dotnet restore against nuget.org must succeed without auth (public feed)");
    }

    [Fact]
    public async Task DotnetTest_TrivialXunitProject_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("dotnet")) return;

        await using var fixture = await FixtureWorkdir.CreateXunitProjectAsync("TestSmoke");
        await using var sandbox = NewSandbox(fixture.Path);

        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "dotnet", new[] { "test", "--nologo", "-v", "minimal" }, timeoutSec: 240),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0,
            "dotnet test on a trivial xUnit project must succeed");
    }

    [Fact]
    public async Task DotnetRestore_PrivateFeedWithPat_Succeeds()
    {
        if (!SandboxToolAvailability.IsAvailable("dotnet")) return;
        var feedUrl = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_NUGET_FEED");
        var token = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_NUGET_TOKEN");
        if (string.IsNullOrWhiteSpace(feedUrl) || string.IsNullOrWhiteSpace(token))
        {
            output.WriteLine("Skipped: AGENTSMITH_TEST_NUGET_FEED + AGENTSMITH_TEST_NUGET_TOKEN not set");
            return;
        }

        await using var fixture = await FixtureWorkdir.CreatePrivateFeedConsoleProjectAsync("PrivateRestore", feedUrl, token);
        await using var sandbox = NewSandbox(fixture.Path);

        var result = await sandbox.RunStepAsync(
            BuildRunStep(sandbox.WorkDir, "dotnet", new[] { "restore", "--nologo", "-v", "minimal" }, timeoutSec: 180),
            progress: null, CancellationToken.None);

        output.WriteLine($"exit={result.ExitCode} err={result.ErrorMessage}");
        result.ExitCode.Should().Be(0,
            "private NuGet feed restore must succeed once PAT is in NuGet.Config — this is the exact path that produced NU1301 in production before p0191");
    }

    private static InProcessSandbox NewSandbox(string workdir) =>
        new(jobId: "test", workDir: workdir, ownsWorkDir: false, NullLogger.Instance);

    private static Step BuildRunStep(string cwd, string cmd, string[] args, int timeoutSec) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd, Args: args, WorkingDirectory: cwd, TimeoutSeconds: timeoutSec);
}
