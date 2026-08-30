using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Output;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-08-30-18e3: a live scan read the middleware next door and grepped for the very
/// configuration key involved, never opened the class where the caller's identity is
/// derived, and said nothing about the gap — the flaw a human found in minutes. The
/// instruction was not missing: the master's first phase already asks for that inventory.
/// The artefact was. These tests hold the artefact to the rule that makes it worth having:
/// a station counts as located only when it cites something this run really read.
/// </summary>
public sealed class RequestStationMapTests : IDisposable
{
    private const string Group = "public REST API";
    private const string ReadFile = "src/Api/Auth/BearerIdentityResolver.cs";

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), $"station-map-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true);
    }

    [Fact]
    public async Task Stations_AMapMissingAStation_RaisesAFindingNamingIt()
    {
        var pipeline = Read(ReadFile);
        StateAll(pipeline, except: VerificationStation.Scope);

        await AccountAsync(pipeline);

        Findings(pipeline).Should().ContainSingle(
            "five stations were located and only the sixth was left unstated")
            .Which.Description.Should()
                .Contain("scope", "the row names the station nothing located")
                .And.Contain(Group, "and the entry group it belongs to")
                .And.Contain("never states this station");
    }

    [Fact]
    public async Task Stations_AStationCitingAFileTheScanCannotRead_CountsAsNotLocated()
    {
        var pipeline = Read(ReadFile);
        StateAll(pipeline);
        // The failure this phase exists for, stated as a claim: a station placed in the file
        // NEXT DOOR — plausible, unread, and until now indistinguishable from an answer.
        Record(pipeline, VerificationStation.Resolution, "src/Api/Middleware/RequestLogger.cs", 44);

        await AccountAsync(pipeline);

        Station(pipeline, VerificationStation.Resolution).Located.Should().BeFalse();
        Station(pipeline, VerificationStation.Resolution).Note.Should()
            .Contain("src/Api/Middleware/RequestLogger.cs").And.Contain("never read");
        Findings(pipeline).Should().ContainSingle().Which.Description.Should().Contain("resolution");
    }

    [Fact]
    public async Task Stations_AStationCitingNothingThatWasRead_IsNotCountedAsLocated()
    {
        // A scan that read nothing at all. Every citation is syntactically a location and
        // none of them resolves, so the map states six gaps rather than six answers.
        var pipeline = Read();
        StateAll(pipeline);

        await AccountAsync(pipeline);

        Map(pipeline).Groups.Should().ContainSingle()
            .Which.Stations.Should().OnlyContain(s => !s.Located);
        Findings(pipeline).Should().HaveCount(Enum.GetValues<VerificationStation>().Length);
    }

    [Fact]
    public async Task Stations_ACompleteMap_RaisesNoFindingAndIsRecorded()
    {
        var pipeline = Read(ReadFile);
        StateAll(pipeline);

        var result = await AccountAsync(pipeline);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("6/6 stations located");
        Findings(pipeline).Should().BeEmpty("a located station is not a finding");
        Map(pipeline).Groups.Should().ContainSingle()
            .Which.Stations.Should().OnlyContain(s => s.Located && s.Display == $"{ReadFile}:12");
    }

    [Fact]
    public async Task Stations_TheMap_IsRenderedIntoTheResultDocument()
    {
        var pipeline = Read(ReadFile);
        StateAll(pipeline, except: VerificationStation.Authority);
        await AccountAsync(pipeline);

        var markdown = await DeliverMarkdownAsync(pipeline);

        markdown.Should().Contain(EntryStationSection.Heading)
            .And.Contain(Group)
            .And.Contain($"`{ReadFile}:12`", "a located station shows where it lives")
            .And.Contain("NOT LOCATED", "and an unlocated one says so where the reader is");
    }

    [Fact]
    public async Task Stations_ARunThatStatedNoMap_DeliversTheDocumentItAlwaysDid()
    {
        var pipeline = Read(ReadFile);

        var result = await AccountAsync(pipeline);

        result.Message.Should().Be("No entry map was stated");
        (await DeliverMarkdownAsync(pipeline)).Should().NotContain(EntryStationSection.Heading);
    }

    [Fact]
    public async Task Stations_AnUnlocatedStation_DoesNotFailTheDeliveryGate()
    {
        // A repository with no ownership model cannot locate a scope station, ever. Routing
        // that row into the ledger would leave it outstanding forever and fail every scan.
        var pipeline = Read(ReadFile);
        StateAll(pipeline, except: VerificationStation.Scope);
        WithSatisfiedScanContract(pipeline);

        await AccountAsync(pipeline);
        await new AccountScanCoverageHandler(
                new ScanCoverageAccountant(), NullLogger<AccountScanCoverageHandler>.Instance)
            .ExecuteAsync(new AccountScanCoverageContext(pipeline), CancellationToken.None);

        var accounts = RunAccountLedger.Current(pipeline);
        accounts.All.SelectMany(a => a.Criteria).Should().NotContain(
            c => c.Criterion.Contains("station", StringComparison.OrdinalIgnoreCase),
            "the map is a reporting surface — it never enters the ledger the gate reads");
        RunDeliveryGate.Evaluate(accounts, ratifiedCriteria: 2).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Stations_AnApiSecurityRun_IsNotAskedForAMap()
    {
        // Three masters declare the observation schema. The api scan runs its source
        // checkout fail-soft and frequently holds no source; a pr review is shown a diff.
        ScanStationToolFactory.Maps("api-security-master").Should().BeFalse();
        ScanStationToolFactory.Maps("pr-review-master").Should().BeFalse();
        ScanStationToolFactory.Maps(PipelinePresets.SecurityMaster).Should().BeTrue();

        new ScanStationToolFactory().For("api-security-master", new PipelineContext())
            .Should().BeEmpty();
        new ScanStationToolFactory().For(PipelinePresets.SecurityMaster, new PipelineContext())
            .Should().ContainSingle().Which.Name.Should().Be(ScanStationToolHost.ToolName);

        PipelinePresets.ApiSecurityScan.Should().NotContain(CommandNames.AccountEntryStations);
        PipelinePresets.PrReview.Should().NotContain(CommandNames.AccountEntryStations);
        PipelinePresets.SecurityScan.Should().Contain(CommandNames.AccountEntryStations);
    }

    [Fact]
    public void Stations_TheObservationArrayContract_IsUnchanged()
    {
        // The map travels as its own tool call for exactly this reason: the master's final
        // answer stays a bare JSON array, so the merge cannot fall to its degraded branch
        // and ship raw untriaged scanner output the way it did on one run in three.
        using var stream = typeof(ObservationParser).Assembly.GetManifestResourceStream(
            "AgentSmith.Application.Services.Validation.Schemas.observation.schema.json");
        stream.Should().NotBeNull();
        using var schema = JsonDocument.Parse(stream!);

        schema.RootElement.GetProperty("type").GetString().Should().Be("array");
        schema.RootElement.TryGetProperty("properties", out _).Should().BeFalse(
            "an object wrapper is what the closed contract refuses");
        schema.RootElement.GetProperty("items").GetProperty("required")
            .EnumerateArray().Select(e => e.GetString())
            .Should().NotContain("stations").And.NotContain("entry_groups");
    }

    // ---- the run under test -------------------------------------------------------

    /// <summary>A run whose master read exactly these paths — the only evidence a station
    /// location is checked against.</summary>
    private static PipelineContext Read(params string[] paths)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, PipelinePresets.SecurityMaster);
        pipeline.Set(ContextKeys.MasterReadPaths, paths.ToList());
        return pipeline;
    }

    /// <summary>The master states every station of one group at the file it read, save one.</summary>
    private static void StateAll(PipelineContext pipeline, VerificationStation? except = null)
    {
        foreach (var station in Enum.GetValues<VerificationStation>())
            if (station != except)
                Record(pipeline, station, ReadFile, 12);
    }

    private static void Record(
        PipelineContext pipeline, VerificationStation station, string file, int line) =>
        new ScanStationToolHost(StationClaimLog.GetOrCreate(pipeline))
            .RecordEntryStation(Group, station.ToString(), file, line);

    private static Task<CommandResult> AccountAsync(PipelineContext pipeline) =>
        new AccountEntryStationsHandler(
                new StationMapResolver(),
                new ScannerObservationFactory(NullLogger<ScannerObservationFactory>.Instance),
                NullLogger<AccountEntryStationsHandler>.Instance)
            .ExecuteAsync(new AccountEntryStationsContext(pipeline), CancellationToken.None);

    private static RequestStationMap Map(PipelineContext pipeline) =>
        pipeline.TryGet<RequestStationMap>(ContextKeys.RequestStationMap, out var map)
        && map is not null ? map : RequestStationMap.Empty;

    private static StationLocation Station(PipelineContext pipeline, VerificationStation station) =>
        Map(pipeline).Groups.Single().Stations.Single(s => s.Station == station);

    private static IReadOnlyList<SkillObservation> Findings(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs)
        && obs is not null ? obs : [];

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
