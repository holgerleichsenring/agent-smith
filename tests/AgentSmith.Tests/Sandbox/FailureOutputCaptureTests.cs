using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using AgentSmith.Tests.Architecture;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0419: a red build has to name its reason.
/// <para>
/// Run 354b failed verification in two repositories and reported both as
/// "build 'dotnet build' exited 1:" followed by a blank line. The sandbox
/// captured stderr only, and MSBuild — like npm and cargo — writes its
/// diagnostics to stdout, so the one thing the next iteration needed was the
/// one thing thrown away.
/// </para>
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public class FailureOutputCaptureTests
{
    [Fact]
    public async Task NonZeroExit_WithDiagnosticsOnStdoutOnly_ReportsThem()
    {
        var result = await RunAsync("echo 'error CS1002: ; expected' && exit 1");

        result.ExitCode.Should().Be(1);
        result.ErrorMessage.Should().Contain("error CS1002",
            "a build tool reports its errors on stdout; capturing stderr alone " +
            "left the run with an exit code and a blank reason");
    }

    [Fact]
    public async Task NonZeroExit_WithDiagnosticsOnStderr_StillReportsThem()
    {
        var result = await RunAsync("echo 'fatal: repository not found' >&2 && exit 128");

        result.ErrorMessage.Should().Contain("repository not found");
    }

    [Fact]
    public async Task ZeroExit_ReportsNoError_EvenWhenTheCommandWasChatty()
    {
        var result = await RunAsync("echo 'warning: chatty' >&2 && echo done");

        result.ExitCode.Should().Be(0);
        result.ErrorMessage.Should().BeNull(
            "chatter on a successful command is not a failure signal");
    }

    /// <summary>
    /// p0419: stdout is the RESULT, not only a live stream. In CLI mode every Run step
    /// returned OutputContent null, so `git diff --cached --name-only` read as "nothing
    /// staged" and run c96d reported "no code changes — no PR" for work it had verified
    /// green one step earlier.
    /// </summary>
    [Fact]
    public async Task ZeroExit_ReturnsWhatTheCommandPrinted_NotOnlyAStream()
    {
        var result = await RunAsync("echo src/Changed.cs; echo src/Other.cs");

        result.ExitCode.Should().Be(0);
        result.OutputContent.Should().NotBeNull(
            "a step that streams but returns nothing is a step no handler can read");
        result.OutputContent.Should().Contain("src/Changed.cs");
        result.OutputContent.Should().Contain("src/Other.cs");
    }

    [Fact]
    public void OutputTail_KeepsTheEnd_BecauseTheErrorSummaryComesLast()
    {
        var tail = new OutputTail(budgetChars: 40);
        foreach (var line in new[] { "restore chatter", "more chatter", "error: the reason" })
            tail.Append(line);

        tail.ToString().Should().Contain("error: the reason");
        tail.ToString().Should().NotContain("restore chatter");
    }

    private static async Task<StepResult> RunAsync(string shellCommand)
    {
        var workDir = Directory.CreateTempSubdirectory("agentsmith-p0419-").FullName;
        try
        {
            var sandbox = new InProcessSandbox(
                jobId: "p0419", workDir, ownsWorkDir: false,
                NullLogger<InProcessSandbox>.Instance);
            var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
                StepKind.Run, Command: "/bin/sh",
                Args: ["-c", shellCommand], TimeoutSeconds: 60);
            return await sandbox.RunStepAsync(step, progress: null, CancellationToken.None);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }
}
