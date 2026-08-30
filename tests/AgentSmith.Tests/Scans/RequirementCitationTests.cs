using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services.Verification;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-08-30-03e1: the standard is what a finding CITES, never an agenda the scan walks.
/// A finding that names a clause carries a place that resolves; a finding no clause covers
/// travels the ordinary path unchanged; a station counts as examined only when its own
/// citation stands and the run read around it; and the entry map underneath is untouched.
/// </summary>
public sealed class RequirementCitationTests : IDisposable
{
    private const string Group = "public REST API";
    private const string ReadFile = "src/Api/Orders/OrderController.cs";
    private const string AlsoRead = "src/Api/Orders/OrderRepository.cs";
    private const string Lonely = "src/Platform/Identity/PrincipalExtensions.cs";
    private const string NeverRead = "src/Api/Middleware/RequestLogger.cs";

    private static readonly IVerificationLens Lens = new AsvsVerificationLens(
        new EmbeddedVerificationCatalogue(new AsvsFlatExportParser()),
        new VerificationLensTableParser());

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), $"requirement-citation-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true);
    }

    [Fact]
    public async Task Citation_AFindingNamingNoRequirement_IsNotDeliveredFromThisPath()
    {
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);

        var refusal = Cite(pipeline, VerificationStation.Authority, requirementId: string.Empty);
        await AccountAsync(pipeline);

        refusal.Should().StartWith("Error:")
            .And.Contain("observation array", "the refusal points the finding somewhere");
        CitedFindingLog.In(pipeline).Should().BeEmpty();
        Findings(pipeline).Should().NotContain(f => f.Role == CitedFindingObservations.Role);
    }

    [Fact]
    public async Task Citation_AFindingNoEntryCovers_IsStillDeliveredOnTheOrdinaryPath()
    {
        // Three of the five findings this phase exists for are of exactly this kind — a
        // logic flaw in an identity helper, a configured id granting administrative rights,
        // a security-shaped flag no code reads. A rule that dropped them would suppress the
        // class it was built to recover.
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        var uncovered = Uncovered();
        Observations().AppendObservations(pipeline, [uncovered]);

        await AccountAsync(pipeline);

        Findings(pipeline).Should().ContainEquivalentOf(uncovered,
            "a finding no entry of the standard covers reaches the reader unchanged");
    }

    [Fact]
    public async Task Citation_AFindingCitingNothingThatWasRead_IsNotCountedAsLocated()
    {
        // The failure the whole track exists for: a claim placed in the file NEXT DOOR —
        // plausible, unread, and until now indistinguishable from work.
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Resolution);
        Cite(pipeline, VerificationStation.Resolution, entry, file: NeverRead, line: 44);

        await AccountAsync(pipeline);

        var station = Station(pipeline, VerificationStation.Resolution);
        station.Located.Should().BeEmpty();
        station.Unlocated.Should().ContainSingle()
            .Which.Note.Should().Contain(NeverRead).And.Contain("never read");
        Findings(pipeline).Should().NotContain(f => f.Role == CitedFindingObservations.Role);
        (await DeliverMarkdownAsync(pipeline)).Should().Contain("NOT DELIVERED",
            "a claim the scan could not carry is reported, never silently dropped");
    }

    [Fact]
    public void Citation_TheLookup_ReturnsTheFullFloorSetNotTwelve()
    {
        var pipeline = Read(ReadFile);

        var listed = new RequirementLookupToolHost(Lens, pipeline).LookUpRequirements("effect");
        var entries = Lens.For(pipeline, VerificationStation.Effect).Requirements;

        entries.Should().HaveCountGreaterThan(12,
            "the twelve-per-station bound paid for an enumeration nobody performs any more; "
            + "kept, it would refuse a real finding against the thirteenth-ranked entry");
        listed.Split('\n').Count(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Should().Be(entries.Count, "the lookup answers with the whole floor set");
        entries.Select(e => e.Level).Should().OnlyContain(level => level == "1" || level == "2");
    }

    [Fact]
    public async Task Citation_AStationWithNoReadFilesBeneathIt_IsNotReportedAsExamined()
    {
        // Examined is not a fresh assertion by the model: the station's own citation must
        // stand AND the run must have read around it. A scan that opened one class and
        // nothing else looked at a station; it did not examine one.
        var pipeline = Read(ReadFile, AlsoRead, Lonely);
        Map(pipeline);
        Locate(pipeline, VerificationStation.Scope, Lonely);

        await AccountAsync(pipeline);

        Station(pipeline, VerificationStation.Admission).Examined.Should().BeTrue(
            "the run read another file beneath the located one");
        var lonely = Station(pipeline, VerificationStation.Scope);
        lonely.Examined.Should().BeFalse();
        lonely.Note.Should().Contain(Lonely).And.Contain("read nothing else beneath it");
    }

    [Fact]
    public async Task Citation_NoStationWasExamined_ReportsThatRatherThanFullCoverage()
    {
        var pipeline = Read(ReadFile);
        Map(pipeline);

        await AccountAsync(pipeline);
        var markdown = await DeliverMarkdownAsync(pipeline);

        Account(pipeline).ExaminedCount.Should().Be(0);
        markdown.Should().Contain(ExaminationSection.Heading)
            .And.Contain("NO STATION WAS EXAMINED")
            .And.Contain("covers none of them",
                "a run that examined nothing says so — a denominator nobody answered still "
                + "reads as a number");
    }

    [Fact]
    public async Task Citation_TheStationMap_BehavesExactlyAsBefore()
    {
        // 2026-08-30-18e3 is the instrument and the external denominator: six stations per
        // group, each owed a location that resolves. Nothing here touches it.
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        new ScanStationToolHost(StationClaimLog.GetOrCreate(pipeline))
            .RecordEntryStation(Group, "scope", string.Empty, 0, "authorization is role-only");

        await AccountAsync(pipeline);
        var markdown = await DeliverMarkdownAsync(pipeline);

        var map = pipeline.Get<RequestStationMap>(ContextKeys.RequestStationMap);
        map.Groups.Should().ContainSingle().Which.Stations.Should().HaveCount(6);
        map.Unlocated.Should().ContainSingle()
            .Which.Station.Note.Should().Be("authorization is role-only");
        Findings(pipeline).Should().ContainSingle(f => f.Role == UnlocatedStationFindings.Role);
        markdown.Should().Contain(EntryStationSection.Heading).And.Contain($"`{ReadFile}:12`");
        ScanStationToolHost.ToolName.Should().Be("record_entry_station");
        PipelinePresets.SecurityScan.Should().Contain(CommandNames.AccountEntryStations);
    }

    [Fact]
    public async Task Citation_AGroupWideFindingCitingNoMembers_IsNotCountedAsLocated()
    {
        // "None of these entry points checks who is asking" has no line of its own. It is
        // the strongest claim a scan can make, so it is settled against every member it
        // generalises over — otherwise it would be the cheapest claim in the report.
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Evidence);
        Cite(pipeline, VerificationStation.Evidence, entry, scope: "group");

        await AccountAsync(pipeline);
        Station(pipeline, VerificationStation.Evidence).Located.Should().BeEmpty();

        Cite(pipeline, VerificationStation.Evidence, entry, scope: "group",
            covers: $"{ReadFile},{AlsoRead}");
        await AccountAsync(pipeline);

        Station(pipeline, VerificationStation.Evidence).Located.Should().ContainSingle()
            .Which.Citation.Should().Contain("2 member(s)").And.Contain(ReadFile);
    }

    [Fact]
    public async Task Citation_AGroupBeyondTheCap_IsRecordedNotAttempted()
    {
        var pipeline = Read(ReadFile, AlsoRead);
        var groups = Enumerable.Range(1, CitedFindingLog.MaxEntryGroups + 1)
            .Select(index => $"entry group {index}").ToList();
        foreach (var group in groups) Map(pipeline, group);
        var entry = EntryId(pipeline, VerificationStation.Scope);
        var refusals = groups.Select(group => Cite(
            pipeline, VerificationStation.Scope, entry, group: group, file: ReadFile, line: 12))
            .ToList();

        await AccountAsync(pipeline);

        refusals[^1].Should().StartWith("Not recorded:")
            .And.Contain("observation array", "the cap never costs a finding");
        var last = groups[^1];
        var beyond = Account(pipeline).Groups.Single(g => g.Group == last);
        beyond.Attempted.Should().BeFalse();
        beyond.Stations.Should().BeEmpty("a group nobody reached has no verdict, not a failing one");
        Findings(pipeline).Should().Contain(f => f.Description.Contains("was not attempted")
            && f.Description.Contains(last));
    }

    [Fact]
    public async Task Citation_ACitedFinding_BecomesAFindingCarryingItsRequirementId()
    {
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Authority);
        Cite(pipeline, VerificationStation.Authority, entry, file: ReadFile, line: 88,
            detail: "the handler checks the role and never the object's owner");

        await AccountAsync(pipeline);
        var markdown = await DeliverMarkdownAsync(pipeline);

        Findings(pipeline).Should().ContainSingle(f => f.Role == CitedFindingObservations.Role)
            .Which.Description.Should().Contain(entry).And.Contain(Group).And.Contain("broken");
        markdown.Should().Contain(entry).And.Contain($"`{ReadFile}:88`").And.Contain("OWASP");
    }

    [Fact]
    public async Task Citation_CitedFindings_NeverEnterTheRunAccounts()
    {
        // A station the scan could not examine for want of an input it was never given
        // would sit outstanding in that ledger forever and fail every scan of every
        // repository it applies to.
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Authority);
        Cite(pipeline, VerificationStation.Authority, entry, file: ReadFile, line: 88);
        WithSatisfiedScanContract(pipeline);

        await AccountAsync(pipeline);
        await new AccountScanCoverageHandler(
                new ScanCoverageAccountant(), NullLogger<AccountScanCoverageHandler>.Instance)
            .ExecuteAsync(new AccountScanCoverageContext(pipeline), CancellationToken.None);

        var accounts = RunAccountLedger.Current(pipeline);
        accounts.All.SelectMany(a => a.Criteria).Should().NotContain(
            c => c.Criterion.Contains("requirement", StringComparison.OrdinalIgnoreCase)
                || c.Criterion.Contains(entry, StringComparison.OrdinalIgnoreCase),
            "the account is a reporting surface — it never enters the ledger the gate reads");
        RunDeliveryGate.Evaluate(accounts, ratifiedCriteria: 2).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Citation_TheFindingsArray_IsUnchanged()
    {
        // The citations travel as their own tool calls for exactly this reason: the
        // master's final answer stays a bare JSON array, so the merge cannot fall to its
        // degraded branch and ship raw untriaged scanner output.
        using var stream = typeof(ObservationParser).Assembly.GetManifestResourceStream(
            "AgentSmith.Application.Services.Validation.Schemas.observation.schema.json");
        stream.Should().NotBeNull();
        using var schema = JsonDocument.Parse(stream!);

        schema.RootElement.GetProperty("type").GetString().Should().Be("array");
        schema.RootElement.TryGetProperty("properties", out _).Should().BeFalse(
            "an object wrapper is what the closed contract refuses");
        var item = schema.RootElement.GetProperty("items");
        item.GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .Should().NotContain("requirement_id").And.NotContain("station");
    }

    [Fact]
    public void Citation_AnApiSecurityRun_IsNotAskedForCitations()
    {
        // The question follows the entry map: an api scan holds no source to locate a
        // station in, and a pr review is shown a diff rather than a system.
        var factory = new ScanRequirementToolFactory(Lens, new CitedFindingRecorder(Lens));

        factory.For("api-security-master", new PipelineContext()).Should().BeEmpty();
        factory.For(PipelinePresets.SecurityMaster, new PipelineContext())
            .Select(tool => tool.Name).Should().BeEquivalentTo(
                [RequirementLookupToolHost.ToolName, CitedFindingToolHost.ToolName]);

        PipelinePresets.ApiSecurityScan.Should().NotContain(CommandNames.AccountRequirementCitations);
        PipelinePresets.PrReview.Should().NotContain(CommandNames.AccountRequirementCitations);
        PipelinePresets.SecurityScan.Should().Contain(CommandNames.AccountRequirementCitations);
    }

    [Fact]
    public void Citation_AnEntryTheStandardDoesNotCarryHere_IsRefused()
    {
        var pipeline = Read(ReadFile);

        var refusal = Cite(pipeline, VerificationStation.Resolution, "V99.9.9",
            file: ReadFile, line: 12);

        refusal.Should().StartWith("Error:")
            .And.Contain(RequirementLookupToolHost.ToolName)
            .And.Contain("observation array");
        CitedFindingLog.In(pipeline).Should().BeEmpty();
    }

    // ---- the run under test -------------------------------------------------------

    /// <summary>A run whose master read exactly these paths — the only evidence a citation
    /// is checked against.</summary>
    private static PipelineContext Read(params string[] paths)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, PipelinePresets.SecurityMaster);
        pipeline.Set(ContextKeys.MasterReadPaths, paths.ToList());
        return pipeline;
    }

    /// <summary>The scan states where every station of one group lives, at a file it read.</summary>
    private static void Map(PipelineContext pipeline, string group = Group)
    {
        var host = new ScanStationToolHost(StationClaimLog.GetOrCreate(pipeline));
        foreach (var station in Enum.GetValues<VerificationStation>())
            host.RecordEntryStation(group, station.ToString(), ReadFile, 12);
    }

    private static void Locate(PipelineContext pipeline, VerificationStation station, string file) =>
        new ScanStationToolHost(StationClaimLog.GetOrCreate(pipeline))
            .RecordEntryStation(Group, station.ToString(), file, 30);

    private static string Cite(
        PipelineContext pipeline, VerificationStation station, string requirementId,
        string group = Group, string detail = "no ownership check on the object it returns",
        string scope = "member", string file = "", int line = 0, string covers = "") =>
        new CitedFindingToolHost(
                new CitedFindingRecorder(Lens), CitedFindingLog.GetOrCreate(pipeline), pipeline)
            .RecordCitedFinding(group, station.ToString(), requirementId, detail, scope, file,
                line, covers);

    private static string EntryId(PipelineContext pipeline, VerificationStation station) =>
        Lens.For(pipeline, station).Requirements[0].Id;

    private static SkillObservation Uncovered() =>
        new(Id: 0, Role: "security-master",
            Concern: ObservationConcern.Security,
            Description: "A security-shaped configuration flag that no code reads",
            Suggestion: "Remove the flag or wire it to the check it names",
            Blocking: false, Severity: ObservationSeverity.Medium, Confidence: 90,
            Rationale: "No standard carries a clause for a setting that does nothing",
            EvidenceMode: EvidenceMode.AnalyzedFromSource, Category: "configuration");

    private static async Task AccountAsync(PipelineContext pipeline)
    {
        await new AccountEntryStationsHandler(
                new StationMapResolver(), Observations(),
                NullLogger<AccountEntryStationsHandler>.Instance)
            .ExecuteAsync(new AccountEntryStationsContext(pipeline), CancellationToken.None);
        await new AccountRequirementCitationsHandler(
                new StationExaminationAccountant(Lens), Observations(),
                NullLogger<AccountRequirementCitationsHandler>.Instance)
            .ExecuteAsync(new AccountRequirementCitationsContext(pipeline), CancellationToken.None);
    }

    private static ScannerObservationFactory Observations() =>
        new(NullLogger<ScannerObservationFactory>.Instance);

    private static ScanExaminationAccount Account(PipelineContext pipeline) =>
        pipeline.TryGet<ScanExaminationAccount>(ContextKeys.ScanExaminationAccount, out var account)
        && account is not null ? account : ScanExaminationAccount.Empty;

    private static StationExamination Station(
        PipelineContext pipeline, VerificationStation station) =>
        Account(pipeline).Groups.Single(g => g.Group == Group).Stations
            .Single(s => s.Station == station);

    private static IReadOnlyList<SkillObservation> Findings(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var observations)
        && observations is not null ? observations : [];

    private static void WithSatisfiedScanContract(PipelineContext pipeline)
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
            Ok(CommandNames.StaticPatternScan), Ok(CommandNames.AgenticMaster),
        });
    }

    private static ExecutionTrailEntry Ok(string command) =>
        new(command, null, true, $"{command}: {ReadFile}", DateTimeOffset.UtcNow, TimeSpan.Zero, null);

    private async Task<string> DeliverMarkdownAsync(PipelineContext pipeline)
    {
        await new MarkdownOutputStrategy(NullLogger<MarkdownOutputStrategy>.Instance)
            .DeliverAsync(new OutputContext("scan", null, Findings(pipeline), null, _outputDir, pipeline));
        return await File.ReadAllTextAsync(Path.Combine(_outputDir, "findings.md"));
    }
}
