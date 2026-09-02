using System.Text.Json.Nodes;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-08-30-03e4: a scan whose master answer could not be read delivered RAW scanner
/// findings and still reported every ratified criterion answered. Measured: three identical
/// runs against one repository delivered 25, 26 and 37 findings (8, 8 and 16 critical) and
/// the one that had lost its triage looked like the thorough one. The degraded branch now
/// records why, the coverage account refuses the triage criterion, and the delivered
/// artefact carries the mark for a reader who never sees the run.
/// </summary>
public sealed class ScanTriageDegradationTests : IDisposable
{
    private const string Master = "security-master";
    private const string Unreadable = "I reviewed the scanners; nothing structured to report.";
    private const string Readable =
        """[{"concern":"security","severity":"high","description":"src/A.cs:10: unsafe","file":"src/A.cs","start_line":10}]""";

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), $"scan-triage-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true);
    }

    [Fact]
    public async Task Degraded_MasterAnswerUnreadable_RecordsAReasonOnTheRun()
    {
        var pipeline = await MergeAsync(Unreadable, "observation");

        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out var reason).Should().BeTrue(
            "the master ran under the observation schema and owed a triage it did not deliver");
        reason.Should().Contain("is not a JSON array");
    }

    [Fact]
    public async Task Degraded_MasterAnswerUnreadable_DeliveredArtefactCarriesTheMark()
    {
        var pipeline = await MergeAsync(Unreadable, "observation");

        var markdown = await DeliverMarkdownAsync(pipeline);
        markdown.Should().StartWith("> **" + ScanTriageNotice.Headline,
            "the mark stands above the finding count the lost triage inflated");

        var invocation = await DeliverSarifAsync(pipeline);
        invocation.Should().NotBeNull("a SARIF consumer reads run health from invocations");
        invocation!["executionSuccessful"]!.GetValue<bool>().Should().BeFalse();
        invocation["toolExecutionNotifications"]![0]!["message"]!["text"]!.GetValue<string>()
            .Should().Contain(ScanTriageNotice.Headline);
    }

    [Fact]
    public async Task Degraded_MasterAnswerUnreadable_TriageCriterionIsNotSatisfied()
    {
        var pipeline = await MergeAsync(Unreadable, "observation");
        WithScanContract(pipeline);

        var account = new ScanCoverageAccountant().Account(pipeline);

        account.Delivered.Should().BeFalse("a scan that did not triage has not delivered");
        var outstanding = account.Outstanding.Should().ContainSingle().Subject;
        outstanding.Criterion.Should().Contain("triaged");
        outstanding.Note.Should().Contain("is not a JSON array", "the account names the reason");
        account.Criteria.Should().Contain(c => c.Criterion.Contains("Secrets and unsafe patterns")
            && c.IsSatisfied, "the scanners that did run still answer for themselves");
    }

    [Fact]
    public async Task Degraded_MasterRanWithANonObservationSchema_IsNotRecordedAsDegraded()
    {
        var pipeline = await MergeAsync(Unreadable, "diff");

        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out _).Should().BeFalse(
            "no triage was ever owed, so nothing degraded — this is the coding path");
        ScanTriageNotice.For(pipeline).Should().BeNull();
    }

    [Fact]
    public async Task Degraded_MasterAnswerReadable_OutcomeAndArtefactAreUnchanged()
    {
        var pipeline = await MergeAsync(Readable, "observation");
        WithScanContract(pipeline);

        pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out _).Should().BeFalse();
        new ScanCoverageAccountant().Account(pipeline).Delivered.Should().BeTrue();

        var markdown = await DeliverMarkdownAsync(pipeline);
        markdown.Should().StartWith("## Agent Smith Security Review")
            .And.NotContain(ScanTriageNotice.Headline);
        (await DeliverSarifAsync(pipeline)).Should().BeNull(
            "a healthy run emits the document it emitted before this phase existed");
    }

    private static async Task<PipelineContext> MergeAsync(string answer, string schema)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, Master);
        pipeline.Set(ContextKeys.MasterAnswer, answer);
        pipeline.Set(ContextKeys.SkillObservations, new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/A.cs", 10, "hard-coded credential", "Desc", 90),
        });
        await new MergeMasterFindingsHandler(
                new StubSchemaResolver(schema),
                TolerantJsonParserFactory.CreateMasterAnswerReader(),
                NullLogger<MergeMasterFindingsHandler>.Instance)
            .ExecuteAsync(new MergeMasterFindingsContext(pipeline), CancellationToken.None);
        return pipeline;
    }

    /// <summary>The contract a security scan ratifies, plus a trail in which every step —
    /// the master included — ran and SUCCEEDED. Only the degraded reason separates the two
    /// outcomes, which is the whole point: the master step is green either way.</summary>
    private static void WithScanContract(PipelineContext pipeline)
    {
        pipeline.Set(ContextKeys.ScanContract, new ScanContract(
        [
            new ScanCriterion("Secrets and unsafe patterns in the working source are identified",
                CommandNames.StaticPatternScan),
            new ScanCriterion("Every candidate finding is triaged by the scan master",
                CommandNames.AgenticMaster),
        ]));
        pipeline.Set(ContextKeys.ExecutionTrail, new List<ExecutionTrailEntry>
        {
            Ok(CommandNames.StaticPatternScan),
            Ok(CommandNames.AgenticMaster),
        });
    }

    private static ExecutionTrailEntry Ok(string command) =>
        new(command, null, true, $"{command} completed", DateTimeOffset.UtcNow, TimeSpan.Zero, null);

    private async Task<string> DeliverMarkdownAsync(PipelineContext pipeline)
    {
        await new MarkdownOutputStrategy(NullLogger<MarkdownOutputStrategy>.Instance)
            .DeliverAsync(Output(pipeline));
        return await File.ReadAllTextAsync(Path.Combine(_outputDir, "findings.md"));
    }

    private async Task<JsonNode?> DeliverSarifAsync(PipelineContext pipeline)
    {
        await new SarifOutputStrategy(NullLogger<SarifOutputStrategy>.Instance)
            .DeliverAsync(Output(pipeline));
        var sarif = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.Combine(_outputDir, "findings.sarif")))!;
        return sarif["runs"]![0]!["invocations"]?[0];
    }

    private OutputContext Output(PipelineContext pipeline)
    {
        var delivered = pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var obs) && obs is not null ? obs : [];
        return new OutputContext("scan", null, delivered, null, _outputDir, pipeline);
    }

    private sealed class StubSchemaResolver(string? schema) : IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }
}
