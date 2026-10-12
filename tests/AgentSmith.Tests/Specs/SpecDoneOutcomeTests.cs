using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.SpecExecution;
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

        progress.Specs.Select(p => p.State).Should().Equal(SpecRunState.Done, SpecRunState.NotStarted);
    }

    [Fact]
    public void SpecOutcomeBlock_MultiLineText_RendersAsParseableYaml()
    {
        var yaml = SpecOutcomeBlock.Render("run-1", "## What this spec delivers\n\n- [x] it works\n", string.Empty);

        var outcome = Outcome(yaml);
        outcome["run"].Should().Be("run-1");
        outcome["delivered"].Should().Be("## What this spec delivers\n\n- [x] it works");
        outcome.Should().NotContainKey("review", "an empty section is not written");
        yaml.Should().Contain("delivered: |-", "multi-line text is a literal block a person can read");
    }

    [Fact]
    public void SpecRecordBody_SpecAndOutcome_AreOneYamlDocument()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        SpecReviewLedger.Record(pipeline, SpecReviewReport.NotTaken("the review call failed"));

        var body = SpecRecordBody.For(Set().Specs[0].Draft, pipeline);

        var doc = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(body);
        doc["spec"].Should().Be(A);
        var outcome = (Dictionary<object, object>)doc[SpecOutcomeBlock.Key];
        outcome["run"].Should().Be("run-2");
        ((string)outcome["review"]).Should().Contain("not reviewed: the review call failed");
    }

    [Fact]
    public async Task SpecDoneFiles_ExecutedSpec_WritesDoneFilesAndNamesThePlannedOnes()
    {
        var mock = $"{SeriesPaths.Planned}/{A}-screen.html";
        var files = new Mock<ISandboxFileReader>();
        files.Setup(f => f.TryReadAsync(mock, It.IsAny<CancellationToken>())).ReturnsAsync("<html/>");
        var spec = Set().Specs[0] with { MockPaths = [mock] };

        var move = await new SpecDoneFiles().WriteAsync(files.Object, spec, "spec: x\n", default);

        move.Written.Should().Equal(
            $"{SeriesPaths.Done}/{A}-first.yaml", $"{SeriesPaths.Done}/{A}-first.md", $"{SeriesPaths.Done}/{A}-screen.html");
        move.Planned.Should().Equal(
            $"{SeriesPaths.Planned}/{A}-first.yaml", $"{SeriesPaths.Planned}/{A}-first.md", mock);
        files.Verify(f => f.WriteAsync($"{SeriesPaths.Done}/{A}-screen.html", "<html/>", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SpecSetPointerRecorder_ExecutedIds_KeptByALaterCommit()
    {
        var store = new InMemorySpecSetPointerStore();
        var recorder = new SpecSetPointerRecorder(store, NullLogger<SpecSetPointerRecorder>.Instance);
        var api = new Dictionary<string, string> { ["api"] = "sha" };

        await recorder.RecordAsync("p", "api", Set() with { Executed = [A] }, api, default, executedIds: [A]);
        await recorder.RecordAsync("p", "api", Set(), new Dictionary<string, string> { ["api"] = "sha-2" }, default);

        var pointer = (await store.GetAsync("p", "azdo-8", default))!;
        pointer.ExecutedIds.Should().Equal([A], "a later commit keeps the ids");
        pointer.ExecutedThrough.Should().Be(1);
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

    // 2026-10-08-e8b9f: a blank companion (an approved spec's) is not written as an empty file.
    [Fact]
    public void SeriesFiles_BlankCompanion_RendersNoMarkdownFile()
    {
        var set = Set() with { Specs = [Set().Specs[0] with { Markdown = string.Empty }, Set().Specs[1]] };

        new SeriesFiles(new SeriesManifest()).Render(set).Select(f => f.Path).Should().Equal(
            SeriesPaths.Manifest(Base), $"{SeriesPaths.Planned}/{A}-first.yaml",
            $"{SeriesPaths.Planned}/{B}-second.yaml", $"{SeriesPaths.Planned}/{B}-second.md");
    }

    [Fact]
    public void SeriesStaleFiles_EmptyCompanionFromEarlierFiling_IsStale()
    {
        var set = Set() with { Specs = [Set().Specs[0] with { Markdown = string.Empty }] };
        var rendered = new SeriesFiles(new SeriesManifest()).Render(set);

        new SeriesStaleFiles().Select([$"{A}-first.yaml", $"{A}-first.md"], set, rendered)
            .Should().Equal($"{SeriesPaths.Planned}/{A}-first.md");
    }

    [Fact]
    public async Task SpecDoneFiles_BlankCompanion_WritesNoDoneMarkdownAndNamesThePlannedOne()
    {
        var files = new Mock<ISandboxFileReader>();
        var spec = Set().Specs[0] with { Markdown = string.Empty };

        var move = await new SpecDoneFiles().WriteAsync(files.Object, spec, "spec: x\n", default);

        move.Written.Should().Equal($"{SeriesPaths.Done}/{A}-first.yaml");
        move.Planned.Should().Equal($"{SeriesPaths.Planned}/{A}-first.yaml", $"{SeriesPaths.Planned}/{A}-first.md");
        files.Verify(f => f.WriteAsync($"{SeriesPaths.Done}/{A}-first.md", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SeriesRecordCommit_Message_NamesTheSeriesTicketAndSpec()
    {
        SeriesRecordCommit.MessageFor(Set(), Set().Specs[0]).Should().Be($"spec: {Base} (azdo-8) {A} executed");
    }

    [Fact]
    public async Task WriteSpecRecord_NoSpec_RecordsNothing()
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        var handler = SeriesDoubles.RecordHandler(factory.Object, new InMemorySpecSetPointerStore());

        var result = await handler.ExecuteAsync(new WriteSpecRecordContext(
            new Repository(new BranchName("main"), "/work"), new PipelineContext(), []), default);

        result.IsSuccess.Should().BeTrue();
        factory.Verify(f => f.Create(It.IsAny<ISandbox>()), Times.Never());
    }

    private static Dictionary<object, object> Outcome(string yaml) =>
        (Dictionary<object, object>)new DeserializerBuilder().Build()
            .Deserialize<Dictionary<string, object>>(yaml)[SpecOutcomeBlock.Key];

    private static SpecSet Set() => new(
        "azdo-8",
        [Spec(A, "first"), Spec(B, "second")],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.Derived,
        Series: Base);

    private static DerivedSpec Spec(string id, string label) => new(
        new SpecDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"\n", []), label, $"# {label}\n", []);
}
