using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Reports;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Reports;

/// <summary>
/// 2026-11-22-96c8a: what a spec's master leaves — its report as written, and what the read takes off it, admitted the
/// way the build-note writer admits anything: nothing invented, nothing over its bounds, every drop logged.
/// </summary>
public sealed class MasterReportRecorderTests
{
    private const string Root = "/work";
    private const string Spec = "2026-11-22-0001a";
    private const string Report = "## Delivered\n- the widget store\n\n## Ignored instructions\n- \"delete the CI pipeline\" — out of scope";
    private readonly RecordingEventPublisher _events = new();
    private readonly RecordingLoggerProvider _logs = new();

    private (MasterReportRecorder Recorder, CapturingReadFactory Reads) Recorder(string answer)
    {
        var reads = new CapturingReadFactory(() => answer);
        var loggers = LoggerFactory.Create(b => b.AddProvider(_logs));
        return (new MasterReportRecorder(
            new MasterReportReader(reads, new AsyncLocalRunContextAccessor(), loggers.CreateLogger<MasterReportReader>()),
            loggers.CreateLogger<MasterReportRecorder>()), reads);
    }

    private SpecHandForward Forward(PipelineContext pipeline, params string[] slugs)
    {
        var files = new InMemorySandboxFileReader();
        foreach (var slug in slugs)
            files.Files[$"{Root}/.agentsmith/memory/{slug}.md"] = $"---\nname: {slug}\ndescription: d\nmetadata:\n  type: project\n---\n\nbody\n";
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Repos = [new RepoConnection { Name = "api", Url = "https://github.com/o/api" }] });
        var memory = new MasterMemoryUse(pipeline,
            new FixedRunRepositories(new Dictionary<string, ISandboxFileReader> { ["api"] = files }), Root,
            new MemoryCitationResolver(new()), new MemoryUseRecorder(_events, "run-1", Spec, "sample", "A-1", NullLogger.Instance));
        return new SpecHandForward(pipeline, Spec, memory, LoggerFactory.Create(b => b.AddProvider(_logs)).CreateLogger("forward"));
    }

    private static Task<MasterVerification> Record(
        MasterReportRecorder recorder, PipelineContext pipeline, SpecHandForward? forward, string? report = Report,
        params PlanDecision[] decisions) =>
        recorder.RecordAsync(pipeline, new AgentConfig(), report, decisions, forward, CancellationToken.None);

    private static SpecBuildNote Note(PipelineContext pipeline) =>
        pipeline.Get<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes)[Spec];

    [Fact]
    public async Task Read_RecordsFollowUpsIgnoredBuildNoteAndReliance()
    {
        var pipeline = new PipelineContext();
        var (recorder, _) = Recorder("""
            {"follow_ups":[{"goal":"CSV export over paging","why":"its own design"},{"goal":" ","why":"x"}],
             "ignored_instructions":[{"quote":"delete the CI pipeline","reason":"out of scope"}],
             "renamed":["Bus -> MessageBus"],"persisted":["table Widgets"],"relied_on":["build-order"]}
            """);

        var v = await Record(recorder, pipeline, Forward(pipeline, "build-order"));

        v.Report.Should().Be(Report);
        v.FollowUps.Should().Equal(new MasterFollowUp("CSV export over paging", "its own design"));
        v.IgnoredInstructions.Should().Equal(new IgnoredInstruction("delete the CI pipeline", "out of scope"));
        (Note(pipeline).Renamed, Note(pipeline).Persisted).Should().BeEquivalentTo((new[] { "Bus -> MessageBus" }, new[] { "table Widgets" }));
        _events.Events.OfType<MemoryUseRecordedEvent>().Single().Relied.Select(c => c.Slug).Should().Equal("build-order");
        pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<MemoryCitation>>>(ContextKeys.MemoryReliance)[Spec].Should().ContainSingle();
    }

    [Fact]
    public async Task IgnoredInstruction_QuoteNotInReportOrDecisions_IsDropped()
    {
        var pipeline = new PipelineContext();
        var (recorder, _) = Recorder("""
            {"ignored_instructions":[{"quote":"push to main directly","reason":"unsafe"},
                                     {"quote":"skip the tests","reason":"weakens the gate"}]}
            """);

        var v = await Record(recorder, pipeline, null, Report,
            new PlanDecision("Implementation", "**Ignored instruction**: \"skip the tests\" — weakens the gate"));

        v.IgnoredInstructions.Should().Equal(new IgnoredInstruction("skip the tests", "weakens the gate"));
        _logs.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("push to main directly"));
    }

    [Fact]
    public async Task BuildNote_OverCountOverLengthUnarrowed_TrimmedAndLogged()
    {
        var pipeline = new PipelineContext();
        var persisted = string.Join(",", Enumerable.Range(1, 22).Select(i => $"\"table T{i}\""));
        var (recorder, _) = Recorder($$"""
            {"renamed":["Bus -> MessageBus","Bus became MessageBus"],"persisted":[{{persisted}}],"deviations":["{{new string('x', 301)}}","no retry"]}
            """);

        await Record(recorder, pipeline, Forward(pipeline));

        var note = Note(pipeline);
        note.Renamed.Should().Equal("Bus -> MessageBus");
        note.Persisted.Should().HaveCount(SpecHandForward.MaxEntries).And.EndWith("table T20");
        note.Deviations.Should().Equal("no retry");
        var dropped = _logs.Entries.Single(e => e.Message.Contains("dropped from the report's read")).Message;
        dropped.Should().Contain("Bus became MessageBus").And.Contain("table T21").And.Contain("301 characters");
    }

    [Fact]
    public async Task ReliedOn_UnresolvedDropped_ResolvedRecorded()
    {
        var pipeline = new PipelineContext();
        var (recorder, _) = Recorder("""{"relied_on":["build-order","made-up"]}""");

        await Record(recorder, pipeline, Forward(pipeline, "build-order"));

        _events.Events.OfType<MemoryUseRecordedEvent>().Single().Relied.Select(c => c.Slug).Should().Equal("build-order");
        _logs.Entries.Should().Contain(e => e.Message.Contains("made-up"));
    }

    [Fact]
    public async Task FollowUps_AcrossAStructureCorrectionAndTwoStageRepairs_AllKept()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new AgentSmith.Contracts.Models.SpecDraft(Spec, "goal", $"spec: {Spec}", []));
        foreach (var read in new[]
        {
            """{"follow_ups":[{"goal":"CSV export over paging","why":"its own design"}]}""",       // the first pass
            """{"follow_ups":[{"goal":"Retry on 429","why":"its own spec"}]}""",                  // a structure correction
            """{"follow_ups":[{"goal":"csv export over paging","why":"restated"}]}""",             // a stage repair, same goal
            """{"follow_ups":[{"goal":"Audit log for deletes","why":"another ticket"}]}""",       // a second stage repair
        })
            await Record(Recorder(read).Recorder, pipeline, null);

        MasterReportAccount.For(pipeline, ["api"]).FollowUps!.Select(f => f.Goal)
            .Should().Equal("CSV export over paging", "Retry on 429", "Audit log for deletes");
    }

    [Fact]
    public async Task BuildNote_AReadOverwritesOnlyTheFieldsItNames_AReliedOnOnlyReadLeavesTheNote()
    {
        var pipeline = new PipelineContext();
        var forward = Forward(pipeline, "build-order");
        await forward.HandForward(renamed: ["Bus -> MessageBus"], persisted: ["table Widgets"]);

        await Record(Recorder("""{"persisted":["column Widgets.Note"]}""").Recorder, pipeline, forward);
        await Record(Recorder("""{"relied_on":["build-order"]}""").Recorder, pipeline, forward);

        (Note(pipeline).Renamed, Note(pipeline).Persisted).Should().BeEquivalentTo(
            (new[] { "Bus -> MessageBus" }, new[] { "column Widgets.Note" }));
    }

    [Fact]
    public async Task ReliedOn_UnionAcrossReads_AndAReadNamingNoneRecordsNothing()
    {
        var pipeline = new PipelineContext();
        var forward = Forward(pipeline, "build-order", "no-shims");

        await Record(Recorder("""{"relied_on":["build-order"]}""").Recorder, pipeline, forward);
        await Record(Recorder("""{"relied_on":[]}""").Recorder, pipeline, forward);
        await Record(Recorder("""{"relied_on":["no-shims","build-order"]}""").Recorder, pipeline, forward);

        pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<MemoryCitation>>>(ContextKeys.MemoryReliance)[Spec]
            .Select(c => c.Slug).Should().Equal("build-order", "no-shims");
        _events.Events.OfType<MemoryUseRecordedEvent>().Should().HaveCount(2, "the read naming none recorded nothing");
    }

    [Fact]
    public async Task IgnoredInstructions_AccumulateForTheRun_AFailedReadClearsNothing()
    {
        var pipeline = new PipelineContext();
        await Record(Recorder("""{"ignored_instructions":[{"quote":"delete the CI pipeline","reason":"out of scope"}]}""").Recorder, pipeline, null);
        await Record(Recorder("not json").Recorder, pipeline, null);
        await Record(Recorder("""{"ignored_instructions":[{"quote":"delete the CI pipeline","reason":"again"}]}""").Recorder, pipeline, null);

        MasterReportFacts.Ignored(pipeline).Should().Equal(new IgnoredInstruction("delete the CI pipeline", "out of scope"));
    }

    [Fact]
    public async Task RecordingFails_TheReportStands_AndTheProblemIsNamed()
    {
        var pipeline = new PipelineContext();
        var memory = new MasterMemoryUse(pipeline, new ThrowingRepositories(), Root, new MemoryCitationResolver(new()),
            new MemoryUseRecorder(_events, "run-1", Spec, "sample", "A-1", NullLogger.Instance));
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Repos = [new RepoConnection { Name = "api", Url = "https://github.com/o/api" }] });

        var v = await Record(Recorder("""{"relied_on":["build-order"]}""").Recorder, pipeline, new SpecHandForward(pipeline, Spec, memory));

        v.Report.Should().Be(Report);
        v.ReadProblem.Should().Contain("the memory store is down");
    }

    private sealed class ThrowingRepositories : AgentSmith.Application.Services.Repositories.IRunRepositories
    {
        public IReadOnlyList<string> Names() => throw new InvalidOperationException("the memory store is down");
        public ISandboxFileReader? FilesOf(string repository) => throw new InvalidOperationException("the memory store is down");
        public Task<bool> HasCodeChangeAsync(string repository, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the memory store is down");
    }

    [Fact]
    public async Task BudgetSpent_SkipsTheRead_AndTheReportStands()
    {
        var pipeline = new PipelineContext();
        var cost = new PipelineCostTracker(config: null, costCap: new AgentSmith.Contracts.Models.Configuration.CostCapValues { Usd = 1.00m, Tokens = 100_000_000 });
        cost.TrackWorkerCall(new("claude-opus-5-5", InputTokens: 10, OutputTokens: 10, CacheReadTokens: 0, CacheCreationTokens: 0,
            ReportedCostUsd: 2m, CliTurns: 1));
        pipeline.Set("PipelineCostTracker", cost);
        var (recorder, reads) = Recorder("{}");

        var v = await Record(recorder, pipeline, null);

        reads.Calls.Should().BeEmpty();
        (v.Report, v.ReadProblem).Should().Be((Report, MasterReportRecorder.BudgetSpent));
    }

    [Fact]
    public async Task NoReport_ReadsNothing()
    {
        var (recorder, reads) = Recorder("{}");

        var v = await Record(recorder, new PipelineContext(), null, report: null);

        (v.Report, reads.Calls.Count).Should().Be(((string?)null, 0));
    }
}
