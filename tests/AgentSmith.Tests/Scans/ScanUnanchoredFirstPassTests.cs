using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-09-01-0e80: the review prompt rendered every raw scanner observation before the
/// master had read a line, and closed with "work your methodology over these scanner
/// inputs and the source". Given a list and a codebase, the cheapest correct-looking
/// behaviour is to work the list — twelve pipeline runs handed the list first found none of
/// the defects a direct probe without one found in three attempts out of four.
/// </summary>
public sealed class ScanUnanchoredFirstPassTests
{
    private const string Master = "security-master";
    private static readonly Repository Repo = new(new BranchName("main"), "https://example.test/repo.git");
    private readonly ScanMasterPromptFactory _prompts = new();

    private const string Unanchored =
        """[{"concern":"security","severity":"high","description":"src/A.cs:10: found on my own","file":"src/A.cs","start_line":10}]""";

    private const string Reconciled =
        """[{"concern":"security","severity":"medium","description":"src/B.cs:20: scanner fact I judge real","file":"src/B.cs","start_line":20}]""";

    [Fact]
    public void ScanPrompt_FirstTurn_CarriesNoScannerFindings()
    {
        var prompt = _prompts.Build(RepoScan(), Repo, ["repo"]);

        prompt.Should().NotContain("Scanner Findings", "the list is an anchor")
            .And.NotContain("hardcoded AWS secret");
        prompt.Should().Contain("Nobody has handed you a list");
        prompt.Should().Contain("SECURITY REVIEW").And.Contain("observation array");
    }

    [Fact]
    public void ScanPrompt_SecondTurn_CarriesTheScannerFindingsForReconciliation()
    {
        var prompt = _prompts.BuildReconciliation(RepoScan());

        prompt.Should().NotBeNull();
        prompt.Should().Contain("Scanner Findings").And.Contain("hardcoded AWS secret");
        prompt.Should().Contain("already covered").And.Contain("dismissed");
        _prompts.BuildReconciliation(new PipelineContext()).Should().BeNull(
            "a scan whose scanners found nothing has no list to reconcile");
    }

    [Fact]
    public async Task ScanMaster_ReconciliationTurn_ContinuesTheSameConversation()
    {
        var loop = new SequencedLoopRunner(Unanchored, Reconciled);
        var context = MasterHandlerFixture.BuildContext(Master, scanMinSourceReads: 0);
        SeedScannerFinding(context.Pipeline);

        await MasterHandlerFixture
            .Build(loop, new MasterHandlerFixture.StubPromptCatalog(Master, "body"), masterSchema: "observation")
            .ExecuteAsync(context, CancellationToken.None);

        loop.SeenRequests.Should().HaveCount(2, "the scanners arrive in a second turn");
        loop.SeenRequests[1].UserPrompt.Should().Contain("Scanner Findings");
        loop.SeenRequests[1].PriorMessages.Should().NotBeNullOrEmpty(
            "the reconciliation runs on the same conversation, so the first pass's reads are in view");
        loop.SeenRequests[1].PriorMessages!.Should().Contain(
            m => m.Text != null && m.Text.Contains("found on my own"));
    }

    [Fact]
    public async Task ScanFinding_RecordsWhichPassProducedIt()
    {
        var loop = new SequencedLoopRunner(Unanchored, Reconciled);
        var context = MasterHandlerFixture.BuildContext(Master, scanMinSourceReads: 0);
        SeedScannerFinding(context.Pipeline);

        await MasterHandlerFixture
            .Build(loop, new MasterHandlerFixture.StubPromptCatalog(Master, "body"), masterSchema: "observation")
            .ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.ScanFindingOrigins, out var origins).Should().BeTrue(
            "if the unanchored pass produces nothing the anchored one would not have, "
            + "this phase is refuted — and that can only be seen if the run says so");
        origins!.Values.Should().Contain(ScanMasterPasses.Unanchored)
            .And.Contain(ScanMasterPasses.Reconciliation);
    }

    [Fact]
    public async Task ScanMerge_UncoveredHighScannerFact_IsStillPromoted()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, Master);
        pipeline.Set(ContextKeys.MasterAnswer, Unanchored);
        pipeline.Set(ContextKeys.SkillObservations, new List<SkillObservation> { HighScannerFact() });

        await new MergeMasterFindingsHandler(
                new StubSchemaResolver("observation"),
                TolerantJsonParserFactory.CreateMasterAnswerReader(),
                NullLogger<MergeMasterFindingsHandler>.Instance)
            .ExecuteAsync(new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var delivered);
        delivered.Should().Contain(o => o.File == "src/Config.cs",
            "the safety net is unchanged — an uncovered High+ scanner fact still ships");
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.UnvouchedFindings, out var unvouched);
        unvouched.Should().ContainSingle();
    }

    [Fact]
    public async Task ApiScan_WhoseInputsAreScannerReportsOnly_IsUnchanged()
    {
        var apiPipeline = ApiScan();

        _prompts.Build(apiPipeline, Repo, ["repo"]).Should()
            .Contain("### Nuclei", "an api scan has no independent source to look at first")
            .And.Contain("Work your methodology over these scanner inputs");
        _prompts.BuildReconciliation(apiPipeline).Should().BeNull(
            "its first turn already carried the reports — a second turn would repeat them");

        var loop = new SequencedLoopRunner(Unanchored);
        var context = MasterHandlerFixture.BuildContext("api-security-master", scanMinSourceReads: 0);
        context.Pipeline.Set(ContextKeys.NucleiResult, Nuclei());

        await MasterHandlerFixture
            .Build(loop, new MasterHandlerFixture.StubPromptCatalog("api-security-master", "body"),
                masterSchema: "observation")
            .ExecuteAsync(context, CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("the api path runs the passes it ran before");
    }

    private static PipelineContext RepoScan()
    {
        var pipeline = new PipelineContext();
        SeedScannerFinding(pipeline);
        return pipeline;
    }

    private static PipelineContext ApiScan()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.NucleiResult, Nuclei());
        return pipeline;
    }

    private static NucleiResult Nuclei() =>
        new([new NucleiFinding("t1", "SQLi", "high", "https://x/orders", null, null)], 10, "");

    private static void SeedScannerFinding(PipelineContext pipeline) =>
        pipeline.Set(ContextKeys.SkillObservations, new List<SkillObservation> { HighScannerFact() });

    private static SkillObservation HighScannerFact() =>
        new(Id: 0, Role: "static-pattern-scanner", Concern: ObservationConcern.Security,
            Description: "hardcoded AWS secret", Suggestion: "", Blocking: false,
            Severity: ObservationSeverity.High, Confidence: 90, File: "src/Config.cs", StartLine: 12,
            EvidenceMode: EvidenceMode.AnalyzedFromSource, Category: "secrets");

    private sealed class StubSchemaResolver(string? schema) : Contracts.Services.IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }

    private sealed class SequencedLoopRunner(params string[] texts) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        private int _call;
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var text = texts[Math.Min(_call, texts.Length - 1)];
            _call++;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
