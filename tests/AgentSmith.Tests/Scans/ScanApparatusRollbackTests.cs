using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Infrastructure.Services.Tools;
using AgentSmith.Infrastructure.Services.Zap;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-08-30-f6cf: the apparatus is gone and stays gone. Three live runs against the
/// pinned catalogue, with the master explicitly instructed and every tool schema in its
/// prompt, produced 54 ReadFile calls and no call to any of the added tools — so the
/// catalogue, the three tools and the two accounting steps were withdrawn.
/// <para>
/// These tests hold the two edges of that withdrawal. Nothing the removed batch added may
/// come back by accident: a preset entry with no handler, or a master told to call a tool
/// the backend no longer offers, is a worse state than either end. And the two honesty
/// phases beside it — a lost triage that says so, a cut-off scanner that does not claim
/// completion — behave exactly as they did, because neither needs a prompt, a tool or a
/// model: both are facts the framework already holds.
/// </para>
/// </summary>
public sealed class ScanApparatusRollbackTests
{
    /// <summary>The step names the removed batch put into the security-scan preset.</summary>
    private static readonly string[] RemovedSteps =
    [
        "AccountEntryStationsCommand",
        "AccountRequirementAnswersCommand",
        "AccountRequirementCitationsCommand",
    ];

    /// <summary>The tools the removed batch put on the scan master's surface.</summary>
    private static readonly string[] RemovedTools =
    [
        "record_entry_station",
        "list_station_requirements",
        "record_requirement_answer",
        "look_up_requirements",
        "record_cited_finding",
    ];

    [Fact]
    public void Rollback_TheScanPresets_HoldNoRemovedStep()
    {
        PipelinePresets.SecurityScan.Should().NotIntersectWith(RemovedSteps,
            "a preset entry whose handler is deleted fails the run at composition");
        PipelinePresets.ApiSecurityScan.Should().NotIntersectWith(RemovedSteps);

        PipelinePresets.SecurityScan.Should().Contain(CommandNames.SubstantiateFindings)
            .And.Contain(CommandNames.AccountScanCoverage,
                "the accountability p0429 gave the scan is not part of this withdrawal");
        PipelinePresets.ApiSecurityScan.Should().Contain(CommandNames.AccountSurfaceDifference,
            "2026-08-30-c6ec stands on the served description and the client call sites, "
            + "never on the catalogue, so it is kept");
    }

    [Fact]
    public async Task Rollback_TheMasterToolSurface_OffersNoRemovedTool()
    {
        var prompts = new MasterHandlerFixture.StubPromptCatalog("security-master", "## Role\nreviewer");
        var loop = new RecordingLoopRunner();

        await MasterHandlerFixture.Build(loop, prompts, masterSchema: "observation", maxSubAgents: 20)
            .ExecuteAsync(
                MasterHandlerFixture.BuildContext("security-master", scanMinSourceReads: 0),
                CancellationToken.None);

        var tools = loop.SeenRequests[0].Tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();
        tools.Should().NotIntersectWith(RemovedTools,
            "the security master is the one that was instructed to call them");
        tools.Should().Contain("read_file").And.Contain("log_decision")
            .And.Contain("spawn_agents", "the scan surface is what it was before the batch");
        tools.Should().NotContain("write_file").And.NotContain("run_command");
    }

    [Fact]
    public void Rollback_ADegradedTriage_StillReportsItself()
    {
        var degraded = new PipelineContext();
        degraded.Set(ContextKeys.ScanTriageDegraded, "the master answer is not a JSON array");

        ScanTriageNotice.For(degraded).Should().Contain(ScanTriageNotice.Headline);
        ScanTriageNotice.Markdown(degraded).Should().Contain(ScanTriageNotice.Headline);
        ScanTriageNotice.Banner(degraded).Should().Contain(ScanTriageNotice.Headline);
        ScanTriageNotice.SarifInvocations(ScanTriageNotice.For(degraded)!)[0]!
            ["executionSuccessful"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Rollback_ACutOffDynamicStep_StillReportsItself()
    {
        const int Limit = 90;
        var cutOff = await ScanWithZapAsync(
            new ToolResult("", "Timeout", null, 1, Limit, CutOff: true), Limit);
        var finished = await ScanWithZapAsync(new ToolResult("", "", null, 0, 3), Limit);

        cutOff.Degraded.Should().BeTrue();
        cutOff.DegradedReason.Should().Contain("cut off").And.Contain($"{Limit}s");
        finished.Degraded.Should().BeFalse();
        finished.DegradedReason.Should().BeNull();

        Account(cutOff).Should().Contain("not evidence of a clean target")
            .And.NotBe(Account(finished),
                "an empty result the step never finished producing is not an empty clean one");
    }

    [Fact]
    public void Rollback_AHealthyScan_BehavesAsBeforeTheBatch()
    {
        var healthy = new PipelineContext();

        ScanTriageNotice.Markdown(healthy).Should().BeEmpty();
        ScanTriageNotice.Banner(healthy).Should().BeEmpty();
        ScanTriageNotice.For(healthy).Should().BeNull(
            "every form of the mark is empty on a run that triaged normally");

        typeof(MarkdownOutputStrategy).Assembly
            .GetType("AgentSmith.Infrastructure.Services.Output.ScanCoverageSections")
            .Should().BeNull("the sections the removed steps rendered are gone with them");

        PipelinePresets.Code.Should().NotIntersectWith(RemovedSteps,
            "the coding preset was never touched by the batch");
    }

    private static string Account(ZapResult zap) =>
        ApiScanFindingsCompressor.BuildSummary(nuclei: null, spectral: null, zap);

    private static Task<ZapResult> ScanWithZapAsync(ToolResult run, int limitSeconds) =>
        new ZapSpawner(
                new FixedToolRunner(run),
                new ZapConfig { ContainerTimeout = limitSeconds },
                new ToolRunnerConfig(),
                NullLogger<ZapSpawner>.Instance)
            .ScanAsync(
                new ZapScanRequest("https://example.test", "api-scan",
                    SwaggerPath: null, AuthToken: null, TimeoutSeconds: limitSeconds),
                CancellationToken.None);

    private sealed class FixedToolRunner(ToolResult result) : IToolRunner
    {
        public Task<ToolResult> RunAsync(ToolRunRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    /// <summary>Keeps the request so the surface the master was actually given can be read.</summary>
    private sealed class RecordingLoopRunner : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
