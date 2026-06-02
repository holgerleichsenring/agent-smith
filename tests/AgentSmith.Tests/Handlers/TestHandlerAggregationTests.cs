using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0202: a per-repo run with exit 0 AND zero discovered tests is a distinct
/// NoTests state, not a failure. Covers dotnet (TRX-empty + exit 0) and jest
/// (no TRX written + exit 0). pytest's exit-5-on-zero-collected stays Fail
/// (documented out: in the phase spec).
/// </summary>
public sealed class TestHandlerAggregationTests
{
    private readonly TestHandler _handler = new(new TrxResultParser(), NullLogger<TestHandler>.Instance);

    [Fact]
    public async Task TestHandler_DotnetOneRepoZeroTests_OthersPass_AggregateNoFailure()
    {
        var context = BuildContext(new()
        {
            ["passing"] = new RepoTestSetup("dotnet test", ExitCode: 0, BuildTrx(passed: 3, failed: 0, total: 3)),
            ["empty"] = new RepoTestSetup("dotnet test", ExitCode: 0, BuildTrx(passed: 0, failed: 0, total: 0)),
        });

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 passed / 1 no-tests / 0 failed");
    }

    [Fact]
    public async Task TestHandler_JestNoTrxExitZero_ClassifiedAsNoTests_NotFail()
    {
        var context = BuildContext(new()
        {
            ["node"] = new RepoTestSetup("jest --passWithNoTests", ExitCode: 0, TrxFiles: null),
        });

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("0 passed / 1 no-tests / 0 failed");
    }

    [Fact]
    public async Task TestHandler_OneRepoActualFailure_AggregateFails()
    {
        var context = BuildContext(new()
        {
            ["good"] = new RepoTestSetup("dotnet test", ExitCode: 0, BuildTrx(passed: 2, failed: 0, total: 2)),
            ["bad"] = new RepoTestSetup("dotnet test", ExitCode: 1, BuildTrx(passed: 1, failed: 1, total: 2)),
        });

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("1 passed / 0 no-tests / 1 failed");
        result.Message.Should().Contain("MyClass.TestA");
    }

    private sealed record RepoTestSetup(string TestCommand, int ExitCode, Dictionary<string, string>? TrxFiles);

    private static TestContext BuildContext(Dictionary<string, RepoTestSetup> repos)
    {
        var pipeline = new PipelineContext();
        var sandboxes = repos.ToDictionary(kv => kv.Key, kv => BuildSandbox(kv.Value), StringComparer.Ordinal);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            repos.ToDictionary(kv => kv.Key, kv => new RemoteContextDiscovery(kv.Key, ".", "polyglot"), StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            repos.ToDictionary(kv => kv.Key, kv => Map(kv.Value.TestCommand), StringComparer.Ordinal));
        var repo = new Repository(new BranchName("main"), "https://github.com/o/r.git");
        return new TestContext(repo, new List<CodeChange>(), pipeline);
    }

    private static ProjectMap Map(string testCommand) => new(
        PrimaryLanguage: "polyglot",
        Frameworks: [],
        Modules: [],
        TestProjects: [],
        EntryPoints: [],
        Conventions: new Conventions(null, null, null),
        Ci: new CiConfig(HasCi: true, BuildCommand: null, TestCommand: testCommand, CiSystem: null));

    private static ISandbox BuildSandbox(RepoTestSetup setup)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(BuildResult(step, setup)));
        return mock.Object;
    }

    private static StepResult BuildResult(Step step, RepoTestSetup setup) =>
        step.Kind switch
        {
            StepKind.Run => new StepResult(StepResult.CurrentSchemaVersion, step.StepId, setup.ExitCode, false, 0.1, null),
            StepKind.ListFiles => new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.0, null,
                OutputContent: JsonSerializer.Serialize((setup.TrxFiles ?? new()).Keys.ToArray(), WireFormat.Json)),
            StepKind.ReadFile => setup.TrxFiles is not null && setup.TrxFiles.TryGetValue(step.Path!, out var content)
                ? new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.0, null, OutputContent: content)
                : new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.0, "missing"),
            _ => new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.0, null)
        };

    private static Dictionary<string, string> BuildTrx(int passed, int failed, int total)
    {
        var failureNodes = string.Join("", Enumerable.Range(0, failed).Select(i =>
            $"<UnitTestResult outcome=\"Failed\" testName=\"MyClass.Test{(char)('A' + i)}\"><Output><ErrorInfo><Message>boom</Message><StackTrace>at X</StackTrace></ErrorInfo></Output></UnitTestResult>"));
        var trx = $"<?xml version=\"1.0\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><ResultSummary><Counters total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" /></ResultSummary><Results>{failureNodes}</Results></TestRun>";
        return new Dictionary<string, string> { ["/work/test-results/r.trx"] = trx };
    }
}
