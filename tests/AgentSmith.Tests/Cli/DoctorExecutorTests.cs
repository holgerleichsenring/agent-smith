using System.Text.Json;
using AgentSmith.Cli.Services.Preflight;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Cli;

/// <summary>
/// p0324: the doctor verb's body — human text with per-check fix hints, --json for
/// CI gating, exit code 0 all green / 1 on any failure.
/// </summary>
public sealed class DoctorExecutorTests
{
    [Fact]
    public async Task DoctorCommand_JsonFlag_EmitsMachineReadableResults()
    {
        var executor = new DoctorExecutor(new ScriptedRunner(ReportWithFailure()));
        var output = new StringWriter();

        var exitCode = await executor.ExecuteAsync(json: true, output, CancellationToken.None);

        exitCode.Should().Be(1);
        using var doc = JsonDocument.Parse(output.ToString());
        doc.RootElement.GetProperty("status").GetString().Should().Be("fail");
        doc.RootElement.GetProperty("exit_code").GetInt32().Should().Be(1);
        var checks = doc.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().HaveCount(2);
        var failed = checks.Single(c => c.GetProperty("status").GetString() == "fail");
        failed.GetProperty("name").GetString().Should().Be("llm-reachable");
        failed.GetProperty("fix_hint").GetString().Should().Contain("api key");
        failed.GetProperty("duration_ms").GetInt64().Should().Be(210);
    }

    [Fact]
    public async Task ExecuteAsync_TextOutput_PrintsFixHintUnderFailure()
    {
        var executor = new DoctorExecutor(new ScriptedRunner(ReportWithFailure()));
        var output = new StringWriter();

        var exitCode = await executor.ExecuteAsync(json: false, output, CancellationToken.None);

        exitCode.Should().Be(1);
        var text = output.ToString();
        text.Should().Contain("[ OK ]").And.Contain("[FAIL]");
        text.Should().Contain("fix: check the api key");
        text.Should().Contain("1 passed, 1 failed, 0 skipped");
    }

    [Fact]
    public async Task ExecuteAsync_AllGreen_ExitZero()
    {
        var report = new PreflightReport([
            new PreflightCheckOutcome("config-schema", "config", PreflightCheckResult.Pass("ok"), 2),
        ]);
        var executor = new DoctorExecutor(new ScriptedRunner(report));

        var exitCode = await executor.ExecuteAsync(json: false, new StringWriter(), CancellationToken.None);

        exitCode.Should().Be(0);
    }

    private static PreflightReport ReportWithFailure() => new([
        new PreflightCheckOutcome("config-schema", "config", PreflightCheckResult.Pass("ok"), 2),
        new PreflightCheckOutcome("llm-reachable", "llm",
            PreflightCheckResult.Fail("401 Unauthorized", "check the api key"), 210),
    ]);

    private sealed class ScriptedRunner(PreflightReport report) : IPreflightRunner
    {
        public Task<PreflightReport> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(report);
    }
}
