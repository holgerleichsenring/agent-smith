using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0324: the runner aggregates in registration order, never throws (a crashing
/// check becomes a failed outcome), and the report's exit code implements the
/// 0-green / 1-any-failure contract.
/// </summary>
public sealed class PreflightRunnerTests
{
    [Fact]
    public async Task PreflightRunner_AllPass_ExitZero()
    {
        var runner = CreateRunner(
            new ScriptedCheck("a", PreflightCheckResult.Pass("ok")),
            new ScriptedCheck("b", PreflightCheckResult.Pass("ok")),
            new ScriptedCheck("c", PreflightCheckResult.Skip("not configured")));

        var report = await runner.RunAsync(CancellationToken.None);

        report.ExitCode.Should().Be(0);
        report.HasFailures.Should().BeFalse();
        report.PassedCount.Should().Be(2);
        report.SkippedCount.Should().Be(1);
        report.Outcomes.Select(o => o.Name).Should().ContainInOrder("a", "b", "c");
    }

    [Fact]
    public async Task RunAsync_AnyFailure_ExitCodeCappedAtOne()
    {
        var runner = CreateRunner(
            new ScriptedCheck("a", PreflightCheckResult.Fail("down", "fix a")),
            new ScriptedCheck("b", PreflightCheckResult.Fail("down", "fix b")));

        var report = await runner.RunAsync(CancellationToken.None);

        report.FailedCount.Should().Be(2);
        report.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_CheckThrows_ReportsFailInsteadOfThrowing()
    {
        var runner = CreateRunner(
            new ThrowingCheck("boom"),
            new ScriptedCheck("after", PreflightCheckResult.Pass("still ran")));

        var report = await runner.RunAsync(CancellationToken.None);

        report.Outcomes.Should().HaveCount(2);
        var crashed = report.Outcomes[0];
        crashed.Result.Status.Should().Be(PreflightStatus.Fail);
        crashed.Result.Message.Should().Contain("boom");
        crashed.Result.FixHint.Should().NotBeNullOrEmpty();
        report.Outcomes[1].Result.Status.Should().Be(PreflightStatus.Pass);
    }

    private static PreflightRunner CreateRunner(params IPreflightCheck[] checks) =>
        new(checks, NullLogger<PreflightRunner>.Instance);

    private sealed class ScriptedCheck(string name, PreflightCheckResult result) : IPreflightCheck
    {
        public string Name => name;

        public string Category => "test";

        public Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingCheck(string message) : IPreflightCheck
    {
        public string Name => "throwing";

        public string Category => "test";

        public Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }
}
