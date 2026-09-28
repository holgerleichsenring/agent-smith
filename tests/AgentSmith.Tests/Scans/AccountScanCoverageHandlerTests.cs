using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// A scan has no CommitAndPR to enforce the delivery gate, so the coverage step does: a
/// scan that could not answer what it ratified fails the run, after recording its account.
/// </summary>
public sealed class AccountScanCoverageHandlerTests
{
    private readonly AccountScanCoverageHandler _sut = new(
        new ScanCoverageAccountant(), NullLogger<AccountScanCoverageHandler>.Instance);

    [Fact]
    public async Task AccountScanCoverage_OutstandingCriterion_FailsWithGateReason()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan), Failed(CommandNames.DependencyAudit));

        var result = await _sut.ExecuteAsync(new AccountScanCoverageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("vulnerable dependencies");
        RunAccountLedger.Current(pipeline).All.Should().ContainSingle(
            "the account is recorded before the step fails, so result.md can name what was missed");
    }

    [Fact]
    public async Task AccountScanCoverage_AllAnswered_Ok()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan), Ok(CommandNames.DependencyAudit));

        var result = await _sut.ExecuteAsync(new AccountScanCoverageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task AccountScanCoverage_NoContract_Ok()
    {
        var result = await _sut.ExecuteAsync(
            new AccountScanCoverageContext(new PipelineContext()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static PipelineContext ScanWith(params ExecutionTrailEntry[] trail)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ScanContract, new ScanContract(
        [
            new ScanCriterion("patterns", CommandNames.StaticPatternScan),
            new ScanCriterion("vulnerable dependencies", CommandNames.DependencyAudit),
        ]));
        pipeline.Set(ContextKeys.ExecutionTrail, trail.ToList());
        return pipeline;
    }

    private static ExecutionTrailEntry Ok(string command) =>
        new(command, null, true, $"{command} found 0", DateTimeOffset.UtcNow, TimeSpan.Zero, null);

    private static ExecutionTrailEntry Failed(string command) =>
        new(command, null, false, "restore returned 401", DateTimeOffset.UtcNow, TimeSpan.Zero, null);
}
