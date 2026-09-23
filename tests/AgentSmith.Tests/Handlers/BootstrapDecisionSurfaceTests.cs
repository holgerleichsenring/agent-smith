using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-19-c511a: the bootstrap round holds a sandbox and no reader factory, so it was one of
/// the three builders that handed the decision logger a repository PATH. It gets the round's own
/// sandbox surface now — the same one its filesystem tool writes context.yaml and the principles
/// through.
/// </summary>
public sealed class BootstrapDecisionSurfaceTests
{
    private const string RunId = "2026-09-19T08-00-00-c511";

    [Fact]
    public async Task BootstrapRound_LogDecision_WritesIntoTheRoundsSandboxTree()
    {
        var sandbox = new RecordingSandbox();
        var runContext = new AsyncLocalRunContextAccessor();
        var factory = new BootstrapToolHostFactory(
            new RepositoryDecisionLogger(
                runContext, new DecisionEventMirror(EventTestStubs.NoOp, runContext),
                NullLogger<RepositoryDecisionLogger>.Instance),
            new SandboxFileReaderFactory(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            ContextGates.Serializer(), ContextGates.Build(), ContextGates.Writer(),
            ContextGates.DerivationStamp());
        using var scope = runContext.BeginScope(RunId);

        // This test exercises log_decision; an empty pipeline carries no discovered contexts,
        // which is the genuine-bootstrap case the name guard passes through.
        var bundle = factory.Create(
            sandbox, "/work", "Sample.Server", "backend", new PipelineContext());
        var logDecision = bundle.Tools.OfType<AIFunction>().Single(t => t.Name == "log_decision");
        await logDecision.InvokeAsync(
            new AIFunctionArguments
            {
                ["category"] = "Tooling",
                ["decision"] = "derived the context from the build file",
            },
            CancellationToken.None);

        sandbox.Written.Should().ContainSingle().Which.Should()
            .Be($".agentsmith/decisions/{RunId}.yaml");
        bundle.GetDecisions().Should().ContainSingle();
    }

    private sealed class RecordingSandbox : ISandbox
    {
        public string JobId => "bootstrap";
        public List<string> Written { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            if (step.Kind == StepKind.WriteFile) Written.Add(step.Path!);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                OutputContent: step.Kind == StepKind.ListFiles ? "[]" : null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
