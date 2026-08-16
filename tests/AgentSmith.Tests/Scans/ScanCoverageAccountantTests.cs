using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: the scan's account of itself, taken mechanically from the execution trail and
/// judged by the ONE delivery gate. A scanner that failed or never ran is NAMED — the
/// damage this answers is a security scan reporting "clean" because its dependency audit
/// died before it could find anything.
/// </summary>
public sealed class ScanCoverageAccountantTests
{
    [Fact]
    public void ScanCoverage_EveryStepRan_IsDelivered()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan), Ok(CommandNames.GitHistoryScan));

        var account = new ScanCoverageAccountant().Account(pipeline);

        account.Delivered.Should().BeTrue();
        account.Criteria.Should().OnlyContain(c => c.Mechanical,
            "no model is asked whether the scan scanned — the trail answers that");
    }

    [Fact]
    public void ScanCoverage_ScannerThatFailed_IsReportedOutstandingNamingTheStep()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan), Failed(CommandNames.GitHistoryScan));

        var account = new ScanCoverageAccountant().Account(pipeline);

        account.Delivered.Should().BeFalse();
        account.Outstanding.Should().ContainSingle()
            .Which.Note.Should().Contain(CommandNames.GitHistoryScan).And.Contain("failed");
    }

    [Fact]
    public void ScanCoverage_StepThatNeverRan_IsOutstanding()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan));

        var account = new ScanCoverageAccountant().Account(pipeline);

        account.Outstanding.Should().ContainSingle()
            .Which.Note.Should().Contain("never ran");
    }

    [Fact]
    public void ScanCoverage_AFailedScanner_FailsTheOneDeliveryGate()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan), Failed(CommandNames.DependencyAudit));
        pipeline.Set(ContextKeys.ScanContract, new ScanContract(
        [
            new ScanCriterion("patterns", CommandNames.StaticPatternScan),
            new ScanCriterion("vulnerable dependencies", CommandNames.DependencyAudit),
        ]));
        RunAccountLedger.Record(pipeline, [new ScanCoverageAccountant().Account(pipeline)]);

        var verdict = RunDeliveryGate.Evaluate(
            RunAccountLedger.Current(pipeline), AcceptanceCriteria.For(pipeline).Count);

        verdict.Satisfied.Should().BeFalse(
            "a scan that could not answer one of its own targets has not delivered");
        verdict.FailureReason.Should().Contain("vulnerable dependencies");
    }

    [Fact]
    public void ScanCoverage_NoContract_AccountsForNothing()
    {
        new ScanCoverageAccountant().Account(new PipelineContext()).Criteria.Should().BeEmpty();
    }

    [Fact]
    public async Task AccountScanCoverage_RecordsTheAccountForTheRunGate()
    {
        var pipeline = ScanWith(Ok(CommandNames.StaticPatternScan), Ok(CommandNames.GitHistoryScan));

        var result = await new AccountScanCoverageHandler(
                new ScanCoverageAccountant(), NullLogger<AccountScanCoverageHandler>.Instance)
            .ExecuteAsync(new AccountScanCoverageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        RunAccountLedger.Current(pipeline).All.Should().ContainSingle()
            .Which.Delivered.Should().BeTrue();
    }

    private static PipelineContext ScanWith(params ExecutionTrailEntry[] trail)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ScanContract, new ScanContract(
        [
            new ScanCriterion("patterns in the working source", CommandNames.StaticPatternScan),
            new ScanCriterion("secrets in history", CommandNames.GitHistoryScan),
        ]));
        pipeline.Set(ContextKeys.ExecutionTrail, trail.ToList());
        return pipeline;
    }

    private static ExecutionTrailEntry Ok(string command) =>
        new(command, null, true, $"{command} found 0", DateTimeOffset.UtcNow, TimeSpan.Zero, null);

    private static ExecutionTrailEntry Failed(string command) =>
        new(command, null, false, "restore returned 401", DateTimeOffset.UtcNow, TimeSpan.Zero, null);
}
