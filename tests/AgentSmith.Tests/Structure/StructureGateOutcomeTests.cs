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
    private static readonly StructureGateVerdict FoundTwo = new(
    [
        new StructureFinding("src/A.cs", "A.Long()", StructureMetric.FunctionLines, 70, 55),
        new StructureFinding("src/B.cs", "B.Wide()", StructureMetric.FunctionLines, 61, 55),
    ]);
    private static readonly StructureGateVerdict NoGrammar = new([], "grammar tree-sitter-c-sharp did not load");

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

        var result = await Outcome().ResolveAsync(pipeline, Found, null, CancellationToken.None);

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
        await Outcome().ResolveAsync(pipeline, Found, null, CancellationToken.None);

        var result = await Outcome().ResolveAsync(pipeline, FoundTwo, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("A.Long()").And.Contain("B.Wide()");
        _progress.Verify(p => p.RecordAsync(pipeline, SpecId, SpecRunState.Failed,
            It.Is<string>(s => s.Contains("src/A.cs A.Long(): function_lines 70 > 55")
                && s.Contains("src/B.cs B.Wide(): function_lines 61 > 55")),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitSpecWork_UnavailableGrammar_FailsNamingItAndRecordsFailed()
    {
        var pipeline = Spec();

        var result = await Outcome().ResolveAsync(pipeline, NoGrammar, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("tree-sitter-c-sharp");
        result.InsertNext.Should().BeNullOrEmpty("a grammar that will not load is not the master's to correct");
        _progress.Verify(p => p.RecordAsync(pipeline, SpecId, SpecRunState.Failed,
            It.Is<string>(s => s.Contains("tree-sitter-c-sharp")), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PipelineContext FixPass()
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
        return pipeline;
    }

    [Fact]
    public async Task CommitSpecWork_FindingsInReviewFixPass_RevertsToVerifiedHead()
    {
        var pipeline = FixPass();

        var result = await Outcome().ResolveAsync(pipeline, Found, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("reverted to the verified state");
        result.InsertNext.Should().BeNullOrEmpty("a fix pass gets no correction of its own");
    }

    [Fact]
    public async Task CommitSpecWork_UnavailableGrammarInReviewFixPass_RevertsToVerifiedHead()
    {
        var pipeline = FixPass();

        var result = await Outcome().ResolveAsync(pipeline, NoGrammar, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        pipeline.Get<string>(ContextKeys.SpecReviewReverted).Should().Contain("tree-sitter-c-sharp");
    }

    [Fact]
    public async Task CommitSpecWork_RevertFails_RecordsFailed()
    {
        var pipeline = Spec();
        SpecReviewFixPass.Prepare(pipeline);

        var result = await Outcome().ResolveAsync(pipeline, Found, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _progress.Verify(p => p.RecordAsync(pipeline, SpecId, SpecRunState.Failed,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitSpecWork_Pass_ClearsStructureFindings()
    {
        var pipeline = Spec();
        await Outcome().ResolveAsync(pipeline, Found, null, CancellationToken.None);

        var result = await Outcome().ResolveAsync(pipeline, StructureGateVerdict.Pass, null, CancellationToken.None);

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
    public void PromptBlocks_StructureFindingsAfterARepair_ReplaceTheRepairBlock()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "criterion 1" });
        pipeline.Set(ContextKeys.StructureFindings, new List<string> { "src/A.cs A.Long(): function_lines 70 > 55" });

        SpecExecutionPromptBlocks.OutstandingCriteria(pipeline).Should().BeEmpty(
            "after a repair the structure block lists what to close and nothing else changes");
        SpecExecutionPromptBlocks.StructureFindings(pipeline).Should().Contain("Change nothing else");
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
