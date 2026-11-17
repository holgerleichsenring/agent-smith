using AgentSmith.Application.Services.Specs.Revision;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>2026-11-17-ec48a: when the master may revise its spec — never inside a repair or fix pass, never with staged
/// work that the spec commit would carry.</summary>
public sealed class SpecRevisionGateTests
{
    [Fact]
    public async Task Refusal_BuildingASpecWithACleanIndex_AdmitsTheRevision()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["shop"] = new IndexSandbox("") });

        (await Gate().RefusalAsync(rig.Pipeline, default)).Should().BeNull();
    }

    [Fact]
    public async Task Refusal_RepairPass_IsRefused()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "x" });

        (await Gate().RefusalAsync(rig.Pipeline, default)).Should().Contain("this pass closes findings");
    }

    [Fact]
    public async Task Refusal_FixPass_IsRefused()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set(ContextKeys.SpecReviewFixPass, true);

        (await Gate().RefusalAsync(rig.Pipeline, default)).Should().Contain("this pass closes findings");
    }

    [Fact]
    public async Task Refusal_StagedWork_NamesTheFiles()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["shop"] = new IndexSandbox("src/Export/Writer.cs\n") });

        (await Gate().RefusalAsync(rig.Pipeline, default)).Should().Contain("shop has staged changes (src/Export/Writer.cs)");
    }

    [Fact]
    public async Task Refusal_NoCurrentSpec_IsRefused()
    {
        var pipeline = new PipelineContext();

        (await Gate().RefusalAsync(pipeline, default)).Should().Contain("not building a spec");
    }

    private static SpecRevisionGate Gate() => new(SpecRevisionRig.Git());

    /// <summary>Answers `git diff --cached --name-only` with the given names.</summary>
    private sealed class IndexSandbox(string staged) : ISandbox
    {
        public string JobId => "gate";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null,
                OutputContent: step.Args is ["diff", "--cached", "--name-only"] ? staged : string.Empty));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
