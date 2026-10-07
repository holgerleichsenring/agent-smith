using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YamlDotNet.Serialization;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-06-03c7e: an executed spec is the spec plus an <c>outcome:</c> block in done/, its
/// standing is read off that directory, and the record lands in the carrying repository only.
/// </summary>
public sealed class SpecDoneOutcomeTests
{
    private const string Base = "2026-10-06-8d8d";
    private const string A = Base + "a";
    private const string B = Base + "b";

    [Fact]
    public void SequenceProgress_ExecutedHead_SeededDone()
    {
        var progress = SpecSequenceProgress.ForSet(Set() with { Executed = [A] });

        progress.Phases.Select(p => p.State).Should().Equal(PhaseRunState.Done, PhaseRunState.NotStarted);
    }

    [Fact]
    public void SpecOutcomeBlock_MultiLineText_RendersAsParseableYaml()
    {
        var yaml = SpecOutcomeBlock.Render("run-1", "## What this phase delivers\n\n- [x] it works\n", string.Empty);

        var outcome = Outcome(yaml);
        outcome["run"].Should().Be("run-1");
        outcome["delivered"].Should().Be("## What this phase delivers\n\n- [x] it works");
        outcome.Should().NotContainKey("review", "an empty section is not written");
        yaml.Should().Contain("delivered: |-", "multi-line text is a literal block a person can read");
    }

    [Fact]
    public void PhaseRecordBody_SpecAndOutcome_AreOneYamlDocument()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        PhaseReviewLedger.Record(pipeline, PhaseReviewReport.NotTaken("the review call failed"));

        var body = PhaseRecordBody.For(Set().Phases[0].Draft, pipeline);

        var doc = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(body);
        doc["spec"].Should().Be(A);
        var outcome = (Dictionary<object, object>)doc[SpecOutcomeBlock.Key];
        outcome["run"].Should().Be("run-2");
        ((string)outcome["review"]).Should().Contain("not reviewed: the review call failed");
    }

    [Fact]
    public void SpecCarryingRepoResolver_Named_TakesTheRecordedRepositoryElseTheFirst()
    {
        RepoConnection[] repos = [new() { Name = "api" }, new() { Name = "web" }];

        SpecCarryingRepoResolver.Named(repos, "WEB")!.Name.Should().Be("web");
        SpecCarryingRepoResolver.Named(repos, null)!.Name.Should().Be("api");
        SpecCarryingRepoResolver.Named(repos, "gone")!.Name.Should().Be("api");
        SpecCarryingRepoResolver.Named([], "web").Should().BeNull();
    }

    [Fact]
    public async Task SpecDoneFiles_ExecutedSpec_WritesDoneFilesAndNamesThePlannedOnes()
    {
        var mock = $"{SeriesPaths.Planned}/{A}-screen.html";
        var files = new Mock<ISandboxFileReader>();
        files.Setup(f => f.TryReadAsync(mock, It.IsAny<CancellationToken>())).ReturnsAsync("<html/>");
        var phase = Set().Phases[0] with { MockPaths = [mock] };

        var move = await new SpecDoneFiles().WriteAsync(files.Object, phase, "spec: x\n", default);

        move.Written.Should().Equal(
            $"{SeriesPaths.Done}/{A}-first.yaml", $"{SeriesPaths.Done}/{A}-first.md", $"{SeriesPaths.Done}/{A}-screen.html");
        move.Planned.Should().Equal(
            $"{SeriesPaths.Planned}/{A}-first.yaml", $"{SeriesPaths.Planned}/{A}-first.md", mock);
        files.Verify(f => f.WriteAsync($"{SeriesPaths.Done}/{A}-screen.html", "<html/>", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SpecSetPointerRecorder_ExecutedThrough_ReadsTheRowElseZero()
    {
        var store = new InMemorySpecSetPointerStore();
        var recorder = new SpecSetPointerRecorder(store, NullLogger<SpecSetPointerRecorder>.Instance);
        (await recorder.ExecutedThroughAsync("p", "azdo-8", default)).Should().Be(0);

        await recorder.RecordAsync("p", new RepoConnection { Name = "api" }, Set(), "sha", default, executedThrough: 1);
        await recorder.RecordAsync("p", new RepoConnection { Name = "api" }, Set(), "sha-2", default);

        (await recorder.ExecutedThroughAsync("p", "azdo-8", default)).Should().Be(1, "a later commit keeps the count");
    }

    [Fact]
    public void SeriesFiles_ExecutedSpec_RendersOnlyTheManifestAndThePlannedTail()
    {
        var rendered = new SeriesFiles(new SeriesManifest()).Render(Set() with { Executed = [A] });

        rendered.Select(f => f.Path).Should().Equal(
            SeriesPaths.Manifest(Base), $"{SeriesPaths.Planned}/{B}-second.yaml", $"{SeriesPaths.Planned}/{B}-second.md");
    }

    [Fact]
    public void SeriesStaleFiles_PlannedCopyOfAnExecutedSpec_IsStale()
    {
        var set = Set() with { Executed = [A] };
        var rendered = new SeriesFiles(new SeriesManifest()).Render(set);

        new SeriesStaleFiles().Select([$"{A}-first.yaml", $"{A}-screen.html", $"{B}-screen.html"], set, rendered)
            .Should().Equal($"{SeriesPaths.Planned}/{A}-first.yaml", $"{SeriesPaths.Planned}/{A}-screen.html");
    }

    [Fact]
    public void SeriesRecordCommit_Message_NamesTheSeriesTicketAndSpec()
    {
        SeriesRecordCommit.MessageFor(Set(), Set().Phases[0]).Should().Be($"spec: {Base} (azdo-8) {A} executed");
    }

    [Fact]
    public async Task WritePhaseRecord_NoPhaseSpec_RecordsNothing()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        var handler = SeriesDoubles.RecordHandler(factory.Object, new InMemorySpecSetPointerStore());

        var result = await handler.ExecuteAsync(new WritePhaseRecordContext(
            new Repository(new BranchName("main"), "/work"), new PipelineContext(), []), default);

        result.IsSuccess.Should().BeTrue();
        factory.Verify(f => f.Create(It.IsAny<ISandbox>()), Times.Never());
    }

    private static Dictionary<object, object> Outcome(string yaml) =>
        (Dictionary<object, object>)new DeserializerBuilder().Build()
            .Deserialize<Dictionary<string, object>>(yaml)[SpecOutcomeBlock.Key];

    private static SpecSet Set() => new(
        "azdo-8",
        [Phase(A, "first"), Phase(B, "second")],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.Derived,
        Series: Base);

    private static SpecPhase Phase(string id, string label) => new(
        new PhaseDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"\n", []), label, $"# {label}\n", []);
}
