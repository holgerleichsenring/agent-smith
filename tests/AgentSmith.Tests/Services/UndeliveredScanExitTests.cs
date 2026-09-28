using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// A scan whose delivery gate is unsatisfied fails the run — the CLI turns that into exit 1 —
/// and the finalizer tail still writes the run result, so the operator reads why.
/// </summary>
public sealed class UndeliveredScanExitTests
{
    [Fact]
    public async Task SecurityScan_UndeliveredScan_FailsTheRunAndStillWritesItsResult()
    {
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = UndeliveredScan();
        var coverage = new Mock<ICommandContext>().Object;
        h.FactoryMock.Setup(f => f.Create(
                It.Is<PipelineCommand>(c => c.Name == CommandNames.AccountScanCoverage), project, pipeline))
            .Returns(coverage);
        h.ExecutorMock.Setup(e => e.ExecuteAsync(coverage, It.IsAny<CancellationToken>()))
            .Returns(() => new AccountScanCoverageHandler(
                    new ScanCoverageAccountant(), NullLogger<AccountScanCoverageHandler>.Instance)
                .ExecuteAsync(new AccountScanCoverageContext(pipeline), CancellationToken.None));
        var runResult = new Mock<ICommandContext>().Object;
        h.FactoryMock.Setup(f => f.Create(
                It.Is<PipelineCommand>(c => c.Name == CommandNames.WriteRunResult), project, pipeline))
            .Returns(runResult);
        h.ExecutorMock.Setup(e => e.ExecuteAsync(runResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("written"));

        var result = await h.Sut.ExecuteAsync(
            [CommandNames.AccountScanCoverage, CommandNames.WriteRunResult],
            project, pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<string>(ContextKeys.FailureReason).Should().Contain("vulnerable dependencies");
        h.ExecutorMock.Verify(e => e.ExecuteAsync(runResult, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PipelineContext UndeliveredScan()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ScanContract, new ScanContract(
            [new ScanCriterion("vulnerable dependencies", CommandNames.DependencyAudit)]));
        pipeline.Set(ContextKeys.ExecutionTrail, new List<ExecutionTrailEntry>
        {
            new(CommandNames.DependencyAudit, null, false, "restore returned 401",
                DateTimeOffset.UtcNow, TimeSpan.Zero, null),
        });
        return pipeline;
    }
}
