using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-09-01-6c32: the gate in front of the findings merge was stricter than the parser it
/// guarded, so an array cut off mid-write — the shape the resilient recovery exists for —
/// was rejected before that recovery could run, and the closing answer was being written
/// under a budget sized for a coding turn.
/// </summary>
public sealed class ScanAnswerRecoveryTests
{
    private const string Master = "security-master";

    /// <summary>An array that hit the output cap mid-object: two complete literals, a third
    /// half-written, and no closing bracket anywhere.</summary>
    private const string CutOff =
        """
        [{"concern":"security","severity":"high","description":"src/A.cs:10: unsafe deserialization","file":"src/A.cs","start_line":10},
         {"concern":"security","severity":"medium","description":"src/B.cs:20: weak hash","file":"src/B.cs","start_line":20},
         {"concern":"security","severity":"high","description":"src/C.cs:30: partial
        """;

    private const string NotFindings = "I reviewed the scanners; nothing structured to report.";

    private const string Readable =
        """[{"concern":"security","severity":"high","description":"src/A.cs:10: unsafe","file":"src/A.cs","start_line":10}]""";

    [Fact]
    public async Task Merge_AnswerCutOffMidArray_RecoversTheFindingsItHolds()
    {
        var pipeline = await MergeAsync(CutOff);

        Delivered(pipeline).Should().HaveCount(2, "the two complete literals survive the cut")
            .And.Contain(o => o.File == "src/A.cs")
            .And.Contain(o => o.File == "src/B.cs");
        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out _).Should().BeFalse(
            "an answer the parser can read is not a lost triage");
    }

    [Fact]
    public async Task Merge_AnswerThatIsNotFindingsAtAll_StillDegrades()
    {
        var pipeline = await MergeAsync(NotFindings);

        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out var reason).Should().BeTrue();
        reason.Should().Contain("is not a JSON array");
        pipeline.TryGet<string>(ContextKeys.ScanTriageRecovered, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Merge_RecoveredAnswer_IsRecordedAsRecovered()
    {
        var recovered = await MergeAsync(CutOff);
        var clean = await MergeAsync(Readable);

        recovered.TryGet<string>(ContextKeys.ScanTriageRecovered, out var note).Should().BeTrue(
            "a salvage must not pass as a clean triage");
        note.Should().Contain("truncated");
        clean.TryGet<string>(ContextKeys.ScanTriageRecovered, out _).Should().BeFalse(
            "a complete array was never salvaged");
    }

    [Fact]
    public async Task ScanMaster_ClosingAnswer_IsWrittenUnderItsOwnOutputBudget()
    {
        var loop = new CapturingLoopRunner();
        var prompts = new MasterHandlerFixture.StubPromptCatalog("api-security-master", "body");

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation")
            .ExecuteAsync(
                MasterHandlerFixture.BuildContext("api-security-master", scanMinSourceReads: 0),
                CancellationToken.None);

        loop.SeenRequests[0].MaxOutputTokensOverride.Should().Be(
            new AgentConfig().ScanMasterMaxOutputTokens,
            "37 findings do not fit in the budget a coding turn is written under");

        var coding = new CapturingLoopRunner();
        await MasterHandlerFixture
            .Build(coding, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"))
            .ExecuteAsync(MasterHandlerFixture.BuildContext("coding-agent-master"), CancellationToken.None);
        coding.SeenRequests[0].MaxOutputTokensOverride.Should().BeNull(
            "the coding master's budget is untouched");
    }

    [Fact]
    public async Task ApiScan_UnreadableMasterAnswer_RecordsADegradationNotAZeroCount()
    {
        var pipeline = await CollectAsync(NotFindings);

        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out var reason).Should().BeTrue(
            "an unreadable api-scan triage was a green run with zero findings");
        reason.Should().Contain("is not a JSON array");
        Delivered(pipeline).Should().BeEmpty();
    }

    [Fact]
    public async Task ApiScan_ReadableMasterAnswer_IsUnchanged()
    {
        var pipeline = await CollectAsync(Readable);

        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out _).Should().BeFalse();
        Delivered(pipeline).Should().ContainSingle(o => o.File == "src/A.cs");
    }

    private static async Task<PipelineContext> MergeAsync(string answer)
    {
        var pipeline = Pipeline(answer);
        pipeline.Set(ContextKeys.SkillObservations, new List<SkillObservation>());
        await new MergeMasterFindingsHandler(
                new StubSchemaResolver("observation"),
                TolerantJsonParserFactory.CreateMasterAnswerReader(),
                NullLogger<MergeMasterFindingsHandler>.Instance)
            .ExecuteAsync(new MergeMasterFindingsContext(pipeline), CancellationToken.None);
        return pipeline;
    }

    private static async Task<PipelineContext> CollectAsync(string answer)
    {
        var pipeline = Pipeline(answer);
        await new CollectMasterFindingsHandler(
                new StubSchemaResolver("observation"),
                TolerantJsonParserFactory.CreateMasterAnswerReader(),
                new ScannerObservationFactory(NullLogger<ScannerObservationFactory>.Instance),
                NullLogger<CollectMasterFindingsHandler>.Instance)
            .ExecuteAsync(new CollectMasterFindingsContext(pipeline), CancellationToken.None);
        return pipeline;
    }

    private static PipelineContext Pipeline(string answer)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, Master);
        pipeline.Set(ContextKeys.MasterAnswer, answer);
        return pipeline;
    }

    private static List<SkillObservation> Delivered(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs)
        && obs is not null ? obs : [];

    private sealed class StubSchemaResolver(string? schema) : IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }

    private sealed class CapturingLoopRunner : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
