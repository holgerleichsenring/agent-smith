using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class TestHandlerTrxTests
{
    [Fact]
    public async Task ExecuteAsync_DotnetProject_PassedAllTests_StoresTrxSummary()
    {
        var trx = BuildTrx(passed: 5, failed: 0, total: 5);
        var sandbox = BuildSandbox(testExitCode: 0,
            trxFiles: new() { ["/work/test-results/r.trx"] = trx });
        var handler = new TestHandler(new TrxResultParser(), NullLogger<TestHandler>.Instance);
        var context = BuildContext(sandbox);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var summary = context.Pipeline.Get<TrxSummary>(ContextKeys.TestResults);
        summary.PassedCount.Should().Be(5);
        summary.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_DotnetProject_FailedTests_ReturnsFailWithFailureNames()
    {
        var trx = BuildTrx(passed: 3, failed: 1, total: 4);
        var sandbox = BuildSandbox(testExitCode: 1,
            trxFiles: new() { ["/work/test-results/r.trx"] = trx });
        var handler = new TestHandler(new TrxResultParser(), NullLogger<TestHandler>.Instance);
        var context = BuildContext(sandbox);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("MyClass.TestA");
        var summary = context.Pipeline.Get<TrxSummary>(ContextKeys.TestResults);
        summary.FailedCount.Should().Be(1);
        summary.Failures.Should().ContainSingle().Which.TestName.Should().Be("MyClass.TestA");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleTrxFiles_AggregatesAcrossProjects()
    {
        var trxA = BuildTrx(passed: 2, failed: 0, total: 2);
        var trxB = BuildTrx(passed: 1, failed: 1, total: 2);
        var sandbox = BuildSandbox(testExitCode: 1, trxFiles: new()
        {
            ["/work/test-results/a.trx"] = trxA,
            ["/work/test-results/b.trx"] = trxB
        });
        var handler = new TestHandler(new TrxResultParser(), NullLogger<TestHandler>.Instance);
        var context = BuildContext(sandbox);

        await handler.ExecuteAsync(context, CancellationToken.None);

        var summary = context.Pipeline.Get<TrxSummary>(ContextKeys.TestResults);
        summary.TotalCount.Should().Be(4);
        summary.PassedCount.Should().Be(3);
        summary.FailedCount.Should().Be(1);
    }

    private static ISandbox BuildSandbox(int testExitCode, Dictionary<string, string> trxFiles)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(BuildResult(step, testExitCode, trxFiles)));
        return mock.Object;
    }

    private static StepResult BuildResult(Step step, int testExitCode, Dictionary<string, string> trxFiles) =>
        step.Kind switch
        {
            StepKind.Run => new StepResult(StepResult.CurrentSchemaVersion, step.StepId, testExitCode, false, 0.1, null),
            StepKind.ListFiles => new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.0, null,
                OutputContent: JsonSerializer.Serialize(trxFiles.Keys.ToArray(), WireFormat.Json)),
            StepKind.ReadFile => trxFiles.TryGetValue(step.Path!, out var content)
                ? new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.0, null, OutputContent: content)
                : new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.0, "missing"),
            _ => new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.0, null)
        };

    private static TestContext BuildContext(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, sandbox);
        pipeline.Set(ContextKeys.ProjectMap, new ProjectMap(
            PrimaryLanguage: "dotnet",
            Frameworks: ["xunit"],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(null, null, null),
            Ci: new CiConfig(false, null, null, null)));
        var repo = new Repository(new BranchName("main"), "https://github.com/o/r.git");
        return new TestContext(repo, new List<CodeChange>(), pipeline);
    }

    private static string BuildTrx(int passed, int failed, int total)
    {
        var failureNodes = string.Join("", Enumerable.Range(0, failed).Select(i =>
            $"<UnitTestResult outcome=\"Failed\" testName=\"MyClass.Test{(char)('A' + i)}\"><Output><ErrorInfo><Message>boom</Message><StackTrace>at X</StackTrace></ErrorInfo></Output></UnitTestResult>"));
        return $"<?xml version=\"1.0\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><ResultSummary><Counters total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" /></ResultSummary><Results>{failureNodes}</Results></TestRun>";
    }
}
