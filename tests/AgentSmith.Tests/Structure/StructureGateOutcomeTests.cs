using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Structure;

public sealed class StructureGateOutcomeTests
{
    private const string SpecId = "2026-10-06-test";
    private static readonly StructureGateVerdict Found = new(
        [new StructureFinding("src/A.cs", "A.Long()", StructureMetric.FunctionLines, 70, 55)]);

    private readonly Mock<ISpecProgressRecorder> _progress = new();

    private StructureGateOutcome Outcome() => new(TestSpecReview.Revert(), _progress.Object);

    private static PipelineContext Spec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft(SpecId, "goal", "spec: x\n", []));
        return pipeline;
    }

    [Fact]
    public async Task CommitSpecWork_FirstFindings_SplicesMasterOpenQuestionsCommit()
    {
        var pipeline = Spec();

        var result = await Outcome().ResolveAsync(pipeline, Found, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext!.Select(c => (c.Name, c.SpecId)).Should().Equal(
            (CommandNames.AgenticMaster, SpecId), (CommandNames.MasterOpenQuestions, SpecId),
            (CommandNames.CommitSpecWork, SpecId));
        pipeline.Get<List<string>>(ContextKeys.StructureFindings).Should().ContainSingle()
            .Which.Should().Contain("A.Long()");
    }

    [Fact]
    public async Task CommitSpecWork_SecondFindings_RecordsFailedAndFails()
    {
        var pipeline = Spec();
        await Outcome().ResolveAsync(pipeline, Found, CancellationToken.None);

        var result = await Outcome().ResolveAsync(pipeline, Found, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("A.Long()");
        _progress.Verify(p => p.RecordAsync(pipeline, SpecId, SpecRunState.Failed,
            It.Is<string>(s => s.StartsWith("structure gate")), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitSpecWork_FindingsInReviewFixPass_RevertsToVerifiedHead()
    {
        var pipeline = Spec();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.VerifiedHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["repo"] = "verified-sha" });
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken(
            [new SpecFinding("repo", "src/A.cs", 4, "rule", "why", "P1")]));
        SpecReviewFixPass.Prepare(pipeline);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo"] = new StubSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>());

        var result = await Outcome().ResolveAsync(pipeline, Found, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("reverted to the verified state");
        result.InsertNext.Should().BeNullOrEmpty("a fix pass gets no correction of its own");
    }

    [Fact]
    public async Task CommitSpecWork_RevertFails_RecordsFailed()
    {
        var pipeline = Spec();
        SpecReviewFixPass.Prepare(pipeline);

        var result = await Outcome().ResolveAsync(pipeline, Found, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _progress.Verify(p => p.RecordAsync(pipeline, SpecId, SpecRunState.Failed,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitSpecWork_Pass_ClearsStructureFindings()
    {
        var pipeline = Spec();
        await Outcome().ResolveAsync(pipeline, Found, CancellationToken.None);

        var result = await Outcome().ResolveAsync(pipeline, StructureGateVerdict.Pass, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<List<string>>(ContextKeys.StructureFindings, out _).Should().BeFalse();
    }

    [Fact]
    public void PromptBlocks_StructureFindings_RendersEachFinding()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.StructureFindings, new List<string> { "src/A.cs A.Long(): function_lines 70 > 55" });

        SpecExecutionPromptBlocks.StructureFindings(pipeline)
            .Should().Contain("STRUCTURE correction").And.Contain("- src/A.cs A.Long(): function_lines 70 > 55");
    }

    [Fact]
    public void SpecRepairScope_Reset_ClearsStructureKeys()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.StructureFindings, new List<string> { "x" });
        pipeline.Set(ContextKeys.StructureGateAttempted, true);

        SpecRepairScope.Reset(pipeline);

        pipeline.TryGet<List<string>>(ContextKeys.StructureFindings, out _).Should().BeFalse();
        pipeline.Flag(ContextKeys.StructureGateAttempted).Should().BeFalse();
    }
}
