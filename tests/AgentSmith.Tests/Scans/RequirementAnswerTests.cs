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
/// 2026-08-30-3c12: the entry map says WHERE each station of each entry group lives; these
/// rules hold what the scan can then SHOW for it. An answer counts when it cites something
/// this run really read, a claim about a whole group counts when it cites the members it
/// generalises over, an entry nobody could decide names the input it lacked, and a group
/// the run never reached says so as a budget fact — never as a verdict.
/// </summary>
public sealed class RequirementAnswerTests : IDisposable
{
    private const string Group = "public REST API";
    private const string ReadFile = "src/Api/Orders/OrderController.cs";
    private const string AlsoRead = "src/Api/Orders/OrderRepository.cs";
    private const string NeverRead = "src/Api/Middleware/RequestLogger.cs";

    private static readonly IVerificationLens Lens = new AsvsVerificationLens(
        new EmbeddedVerificationCatalogue(new AsvsFlatExportParser()),
        new VerificationLensTableParser());

    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(), $"requirement-account-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true);
    }

    [Fact]
    public async Task Requirements_AnUnmetEntry_BecomesAFindingCarryingItsRequirementId()
    {
        var pipeline = Read(ReadFile);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Authority);
        Answer(pipeline, VerificationStation.Authority, entry, "unmet", file: ReadFile, line: 88);

        await AccountAsync(pipeline);

        Row(pipeline, VerificationStation.Authority, entry).Disposition
            .Should().Be(RequirementDisposition.Unmet);
        Findings(pipeline).Should().ContainSingle(
            "the map is complete, so the account is the only thing with anything to say")
            .Which.Description.Should()
                .Contain(entry, "the row carries the id of the requirement it answers")
                .And.Contain(Group).And.Contain("not met");
    }

    [Fact]
    public async Task Requirements_APerMemberAnswerCitingNothingThatWasRead_IsNotCountedAsAnswered()
    {
        // The failure the whole track exists for, stated as an answer: a verdict placed in
        // the file NEXT DOOR — plausible, unread, and until now indistinguishable from work.
        var pipeline = Read(ReadFile);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Resolution);
        Answer(pipeline, VerificationStation.Resolution, entry, "met", file: NeverRead, line: 44);

        await AccountAsync(pipeline);

        var row = Row(pipeline, VerificationStation.Resolution, entry);
        row.Answered.Should().BeFalse();
        row.Disposition.Should().Be(RequirementDisposition.Unanswered);
        row.Note.Should().Contain(NeverRead).And.Contain("never read");
        Findings(pipeline).Should().BeEmpty("an answer that resolves against nothing is silence");
    }

    [Fact]
    public async Task Requirements_AGroupWideClaimCitingNoMembers_IsNotCountedAsAnswered()
    {
        // "No entry point here is anonymous" has no line of its own. It is the strongest
        // claim a scan can make, so it is settled against the members it generalises over —
        // otherwise it would be the cheapest claim in the report to fabricate.
        var pipeline = Read(ReadFile, AlsoRead);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Evidence);
        Answer(pipeline, VerificationStation.Evidence, entry, "met", scope: "group");

        await AccountAsync(pipeline);

        var row = Row(pipeline, VerificationStation.Evidence, entry);
        row.Answered.Should().BeFalse();
        row.Note.Should().Contain("cites none of the members");

        Answer(pipeline, VerificationStation.Evidence, entry, "met", scope: "group",
            covers: $"{ReadFile},{AlsoRead}");
        await AccountAsync(pipeline);

        var counted = Row(pipeline, VerificationStation.Evidence, entry);
        counted.Disposition.Should().Be(RequirementDisposition.Met);
        counted.Scope.Should().Be(RequirementScope.GroupWide);
        counted.Citation.Should().Contain("2 member(s)").And.Contain(ReadFile);
    }

    [Fact]
    public async Task Requirements_ACannotAnswerEntry_NamesTheMissingInput()
    {
        const string missing = "the reverse-proxy configuration this service is deployed behind";
        var pipeline = Read(ReadFile);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Admission);

        Answer(pipeline, VerificationStation.Admission, entry, "cannot_answer")
            .Should().StartWith("Error:", "a cannot-answer with no named input is silence");
        Answer(pipeline, VerificationStation.Admission, entry, "cannot_answer", missing: missing);
        await AccountAsync(pipeline);

        Row(pipeline, VerificationStation.Admission, entry).Disposition
            .Should().Be(RequirementDisposition.CannotAnswer);
        Findings(pipeline).Should().ContainSingle()
            .Which.Description.Should().Contain(entry).And.Contain(missing);
    }

    [Fact]
    public async Task Requirements_AGroupBeyondTheCap_IsRecordedNotAttempted()
    {
        var pipeline = Read(ReadFile);
        var groups = Enumerable.Range(1, RequirementAnswerLog.MaxEntryGroups + 1)
            .Select(i => $"entry group {i}").ToList();
        foreach (var group in groups) Map(pipeline, group);
        var entry = EntryId(pipeline, VerificationStation.Scope);
        var refusals = groups.Select(group => Answer(
            pipeline, VerificationStation.Scope, entry, "met", group: group,
            file: ReadFile, line: 12)).ToList();

        await AccountAsync(pipeline);

        var last = groups[^1];
        refusals[^1].Should().StartWith("Not recorded:", "the cap is stated where it binds");
        var beyond = Account(pipeline).Groups.Single(g => g.Group == last);
        beyond.Attempted.Should().BeFalse();
        beyond.Rows.Should().BeEmpty("a group nobody reached has no verdict, not a failing one");
        Account(pipeline).Groups.Count(g => g.Attempted)
            .Should().Be(RequirementAnswerLog.MaxEntryGroups);
        Findings(pipeline).Should().Contain(f => f.Description.Contains("was not attempted")
            && f.Description.Contains(last));
    }

    [Fact]
    public async Task Requirements_AGroupScopedOnReadAndUnscopedOnWrite_IsReported()
    {
        // The asymmetry a reviewer who follows only the read path never reaches: the same
        // resource checked against the caller on the way out and not on the way in.
        var pipeline = Read(ReadFile);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Scope);
        Answer(pipeline, VerificationStation.Scope, entry, "met", file: ReadFile, line: 30);
        Answer(pipeline, VerificationStation.Scope, entry, "unmet", operation: "write",
            file: ReadFile, line: 88);

        await AccountAsync(pipeline);

        var group = Account(pipeline).Groups.Single();
        group.EnumeratesWrites.Should().BeTrue();
        group.ReadWriteAsymmetries.Should().ContainSingle()
            .Which.RequirementId.Should().Be(entry);
        Findings(pipeline).Should().Contain(f =>
            f.Description.Contains("holds on reads but not on writes") && f.Description.Contains(entry));
    }

    [Fact]
    public async Task Requirements_UnmetAndCannotAnswerRows_NeverEnterTheRunAccounts()
    {
        // An entry undecidable for want of an input the scan was never given would sit
        // outstanding in that ledger forever and fail every scan of every repository.
        var pipeline = Read(ReadFile);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Authority);
        Answer(pipeline, VerificationStation.Authority, entry, "unmet", file: ReadFile, line: 88);
        Answer(pipeline, VerificationStation.Authority, EntryId(pipeline, VerificationStation.Authority, 1),
            "cannot_answer", missing: "the ownership model of the objects it returns");
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
    public void Requirements_TheFindingsArray_IsUnchanged()
    {
        // The answers travel as their own tool calls for exactly this reason: the master's
        // final answer stays a bare JSON array, so the merge cannot fall to its degraded
        // branch and ship raw untriaged scanner output the way it did on one run in three.
        using var stream = typeof(ObservationParser).Assembly.GetManifestResourceStream(
            "AgentSmith.Application.Services.Validation.Schemas.observation.schema.json");
        stream.Should().NotBeNull();
        using var schema = JsonDocument.Parse(stream!);

        schema.RootElement.GetProperty("type").GetString().Should().Be("array");
        schema.RootElement.TryGetProperty("properties", out _).Should().BeFalse(
            "an object wrapper is what the closed contract refuses");
        var item = schema.RootElement.GetProperty("items");
        item.GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .Should().NotContain("requirements").And.NotContain("answers");
        item.GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .Should().NotContain("requirement_id").And.NotContain("station");
    }

    [Fact]
    public async Task Requirements_TheAccount_IsRenderedIntoTheResultDocument()
    {
        var pipeline = Read(ReadFile);
        Map(pipeline);
        var entry = EntryId(pipeline, VerificationStation.Admission);
        Answer(pipeline, VerificationStation.Admission, entry, "unmet", file: ReadFile, line: 12);
        await AccountAsync(pipeline);

        var markdown = await DeliverMarkdownAsync(pipeline);

        markdown.Should().Contain(RequirementSection.Heading)
            .And.Contain(Group)
            .And.Contain(entry, "a reader can look the entry up in the standard")
            .And.Contain("No state-changing operation was enumerated",
                "a group that answered only its reads does not read as complete")
            .And.Contain("OWASP", "the ingested text travels with its attribution");
    }

    [Fact]
    public void Requirements_AnApiSecurityRun_IsNotAskedForRequirements()
    {
        // The question follows the entry map: an api scan holds no source to locate a
        // station in, and a pr review is shown a diff rather than a system.
        var factory = new ScanRequirementToolFactory(Lens, new RequirementAnswerRecorder(Lens));

        factory.For("api-security-master", new PipelineContext()).Should().BeEmpty();
        factory.For(PipelinePresets.SecurityMaster, new PipelineContext())
            .Select(t => t.Name).Should().BeEquivalentTo(
                [RequirementCatalogueToolHost.ToolName, RequirementAnswerToolHost.ToolName]);

        PipelinePresets.ApiSecurityScan.Should().NotContain(CommandNames.AccountRequirementAnswers);
        PipelinePresets.PrReview.Should().NotContain(CommandNames.AccountRequirementAnswers);
        PipelinePresets.SecurityScan.Should().Contain(CommandNames.AccountRequirementAnswers);
    }

    [Fact]
    public void Requirements_AnEntryTheStationWasNotHanded_IsRefused()
    {
        // The model answers the entries it is handed and does not choose them — that is what
        // keeps the denominator external.
        var pipeline = Read(ReadFile);

        var listed = new RequirementCatalogueToolHost(Lens, pipeline)
            .ListStationRequirements("resolution");
        var refusal = Answer(pipeline, VerificationStation.Resolution, "V99.9.9", "met",
            file: ReadFile, line: 12);

        listed.Should().Contain(EntryId(pipeline, VerificationStation.Resolution))
            .And.Contain("OWASP");
        refusal.Should().StartWith("Error:").And.Contain("list_station_requirements");
        RequirementAnswerLog.In(pipeline).Should().BeEmpty();
    }

    // ---- the run under test -------------------------------------------------------

    /// <summary>A run whose master read exactly these paths — the only evidence an answer
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

    private static string Answer(
        PipelineContext pipeline, VerificationStation station, string requirementId, string verdict,
        string group = Group, string operation = "read", string scope = "member",
        string file = "", int line = 0, string covers = "", string missing = "") =>
        new RequirementAnswerToolHost(
                new RequirementAnswerRecorder(Lens), RequirementAnswerLog.GetOrCreate(pipeline), pipeline)
            .RecordRequirementAnswer(
                group, station.ToString(), requirementId, operation, verdict, scope, file, line,
                covers, missing);

    private static string EntryId(PipelineContext pipeline, VerificationStation station, int index = 0) =>
        Lens.For(pipeline, station).Requirements[index].Id;

    private static async Task<CommandResult> AccountAsync(PipelineContext pipeline)
    {
        await new AccountEntryStationsHandler(
                new StationMapResolver(), Observations(),
                NullLogger<AccountEntryStationsHandler>.Instance)
            .ExecuteAsync(new AccountEntryStationsContext(pipeline), CancellationToken.None);
        return await new AccountRequirementAnswersHandler(
                new RequirementAccountant(Lens), Observations(),
                NullLogger<AccountRequirementAnswersHandler>.Instance)
            .ExecuteAsync(new AccountRequirementAnswersContext(pipeline), CancellationToken.None);
    }

    private static ScannerObservationFactory Observations() =>
        new(NullLogger<ScannerObservationFactory>.Instance);

    private static RequirementAccount Account(PipelineContext pipeline) =>
        pipeline.TryGet<RequirementAccount>(ContextKeys.RequirementAccount, out var account)
        && account is not null ? account : RequirementAccount.Empty;

    private static RequirementRow Row(
        PipelineContext pipeline, VerificationStation station, string requirementId) =>
        Account(pipeline).Groups.Single(g => g.Group == Group).Rows.Single(
            r => r.Station == station && r.RequirementId == requirementId
                && r.Operation == RequirementOperation.Read);

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
