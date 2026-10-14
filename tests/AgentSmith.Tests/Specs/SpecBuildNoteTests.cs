using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using YamlDotNet.Serialization;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-13-8762m: the build note — hand_forward writes it, the record keeps it, the next spec's
/// prompts read it back from this run or from the record.
/// </summary>
public sealed class SpecBuildNoteTests
{
    private const string First = "2026-10-14-5678a";
    private const string Second = "2026-10-14-5678b";
    private const string Third = "2026-10-14-5678c";

    [Fact]
    public void HandForward_SecondCall_ReplacesTheNote()
    {
        var pipeline = new PipelineContext();
        var host = new HandForwardToolHost(pipeline, First);

        host.HandForward(renamed: ["Bus -> MessageBus"], persisted: ["table RunDefaults"]);
        var answer = host.HandForward(moved: ["src/A.cs -> src/Bus/A.cs"]);

        answer.Should().StartWith("Build note for " + First + " recorded");
        var note = Notes(pipeline)[First];
        note.Moved.Should().Equal("src/A.cs -> src/Bus/A.cs");
        note.Renamed.Should().BeEmpty("the last call wins");
        note.Persisted.Should().BeEmpty();
    }

    [Fact]
    public void HandForward_OverBound_IsRefusedByName()
    {
        var pipeline = new PipelineContext();
        var host = new HandForwardToolHost(pipeline, First);

        var tooMany = host.HandForward(persisted: [.. Enumerable.Range(1, 21).Select(i => $"table T{i}")]);
        var tooLong = host.HandForward(deviations: [new string('x', 301)]);

        tooMany.Should().StartWith("Error").And.Contain("'persisted' has 21 entries, at most 20");
        tooLong.Should().StartWith("Error").And.Contain("'deviations' entry 1 is 301 characters, at most 300");
        pipeline.Has(ContextKeys.SpecBuildNotes).Should().BeFalse("a refused call records nothing");
    }

    [Fact]
    public void HandForward_RenamedEntryWithoutArrow_IsRefusedByName()
    {
        var pipeline = new PipelineContext();

        var answer = new HandForwardToolHost(pipeline, First)
            .HandForward(renamed: ["Bus -> MessageBus", "Bus became MessageBus"], moved: ["a ->b"]);

        answer.Should().StartWith("Error")
            .And.Contain("'renamed' entry 2 \"Bus became MessageBus\" is not '<old> -> <new>'")
            .And.Contain("'moved' entry 1 \"a ->b\" is not '<old> -> <new>'");
        pipeline.Has(ContextKeys.SpecBuildNotes).Should().BeFalse();
    }

    [Fact]
    public void SpecOutcomeBlock_WithNote_RendersFourLists()
    {
        var note = new SpecBuildNote(First, ["Bus -> MessageBus"], [], ["table RunDefaults"], ["kept the old route: a client pins it"]);

        var yaml = SpecOutcomeBlock.Render("run-1", "delivered", string.Empty, new SpecBuildNoteRead(note, false));

        var handForward = (Dictionary<object, object>)Outcome(yaml)[SpecBuildNoteReader.Key];
        ((List<object>)handForward["renamed"]).Should().Equal("Bus -> MessageBus");
        ((List<object>)handForward["moved"]).Should().BeEmpty();
        ((List<object>)handForward["persisted"]).Should().Equal("table RunDefaults");
        ((List<object>)handForward["deviations"]).Should().Equal("kept the old route: a client pins it");
    }

    [Fact]
    public void SpecRecordBody_MasterRanWithoutNote_RecordsMissing()
    {
        var pipeline = Pipeline(executed: []);

        var body = SpecRecordBody.For(Draft(First), pipeline);

        Outcome(body)[SpecBuildNoteReader.Key].Should().Be(SpecBuildNoteReader.Missing);
        SpecBuildNoteReader.Read(body, First).Should().Be(SpecBuildNoteRead.NoNote);
    }

    [Fact]
    public void SpecRecordBody_AlreadySatisfiedRow_RecordsNoHandForwardKey()
    {
        var pipeline = Pipeline(executed: []);
        pipeline.Set(ContextKeys.SpecSequenceProgress, SpecSequenceProgress.ForSet(pipeline.Get<SpecSet>(ContextKeys.SpecSet))
            .With(First, SpecRunState.Done, note: SelectSpecHandler.AlreadySatisfiedNote));

        var body = SpecRecordBody.For(Draft(First), pipeline);

        Outcome(body).Should().NotContainKey(SpecBuildNoteReader.Key, "a spec that ran no master owes no note");
    }

    [Fact]
    public void SpecBuildNoteReader_ExecutedSpecYaml_ReadsTheNote()
    {
        var pipeline = Pipeline(executed: []);
        new HandForwardToolHost(pipeline, First).HandForward(renamed: ["Bus -> MessageBus"], deviations: ["no retry"]);

        var read = SpecBuildNoteReader.Read(SpecRecordBody.For(Draft(First), pipeline), First);

        read.Missing.Should().BeFalse();
        read.Note!.Renamed.Should().Equal("Bus -> MessageBus");
        read.Note.Deviations.Should().Equal("no retry");
        SpecBuildNoteReader.Read("spec: x\ngoal: y\n", First).Should().Be(SpecBuildNoteRead.None);
        SpecBuildNoteReader.Read("::: not yaml", First).Should().Be(SpecBuildNoteRead.None);
    }

    [Fact]
    public void PrecedingSpecs_NotesOf_ContextFirstThenRecordThenNoNote()
    {
        // First ran in an earlier attempt and carries its note in its record; Second ran in this run
        // with a note in the context key; Third is current. A context note wins over a record.
        var recorded = RecordWith(First, ["Old -> New"]);
        var pipeline = Pipeline(executed: [First], firstYaml: recorded, withThird: true);
        pipeline.Set(ContextKeys.SpecSequenceProgress, SpecSequenceProgress.ForSet(pipeline.Get<SpecSet>(ContextKeys.SpecSet))
            .With(Second, SpecRunState.Done));
        new HandForwardToolHost(pipeline, Second).HandForward(persisted: ["column Runs.Note"]);

        var notes = PrecedingSpecs.NotesOf(pipeline, Third);

        notes.Select(n => n.SpecId).Should().Equal(First, Second);
        notes[0].Note!.Renamed.Should().Equal("Old -> New");
        notes[0].DoneFile.Should().Be($"{SeriesPaths.Done}/{First}-first.yaml");
        notes[1].Note!.Persisted.Should().Equal("column Runs.Note");

        new HandForwardToolHost(pipeline, First).HandForward(renamed: ["Context -> Wins"]);
        PrecedingSpecs.NotesOf(pipeline, Third)[0].Note!.Renamed.Should().Equal("Context -> Wins");

        var bare = Pipeline(executed: [First], withThird: true);
        PrecedingSpecs.NotesOf(bare, Second).Single().Note.Should().BeNull("no context note and no record note");
        SpecBuildNoteSection.Render(PrecedingSpecs.NotesOf(bare, Second)).Should().Contain(SpecBuildNoteSection.NoNote);
    }

    [Fact]
    public void SpecBuildNoteSection_OverBound_DropsOldestAndNamesTheirFiles()
    {
        var notes = Enumerable.Range(1, 5).Select(i => new PrecedingNote(
            $"2026-10-14-000{i}", $".agentsmith/specs/done/2026-10-14-000{i}-x.yaml",
            new SpecBuildNote($"2026-10-14-000{i}", [], [], [], [new string((char)('a' + i), 250)]))).ToList();

        var text = SpecBuildNoteSection.Render(notes, maxChars: 700);

        text.Length.Should().BeLessThanOrEqualTo(700);
        text.Should().Contain("### 2026-10-14-0005").And.NotContain("### 2026-10-14-0001");
        text.Should().Contain("2026-10-14-0001 (.agentsmith/specs/done/2026-10-14-0001-x.yaml)");
        SpecBuildNoteSection.Render(notes).Should().NotContain("Dropped", "five short notes fit the default bound");
    }

    [Fact]
    public void SpecExecutionPrompt_SecondSpec_ShowsFirstSpecsNote()
    {
        var pipeline = Pipeline(executed: []);
        pipeline.Set(ContextKeys.SpecSequenceProgress, SpecSequenceProgress.ForSet(pipeline.Get<SpecSet>(ContextKeys.SpecSet))
            .With(First, SpecRunState.Done));
        new HandForwardToolHost(pipeline, First).HandForward(renamed: ["Bus -> MessageBus"]);
        pipeline.Set(ContextKeys.CurrentSpec, Draft(Second));
        pipeline.Set(ContextKeys.MemoryIndex, "- [bus-name](bus-name.md) — the bus is MessageBus now");

        var prompt = new SpecExecutionPromptFactory().Build(pipeline,
            new Ticket(new TicketId("7"), "t", "d", null, "open", "test"),
            new Repository(new BranchName("main"), "https://example.test/shop.git"), ["shop"], string.Empty, string.Empty);

        prompt.Should().Contain(SpecBuildNoteSection.Heading).And.Contain($"### {First}")
            .And.Contain("- renamed: Bus -> MessageBus")
            .And.Contain("## Experiential memory index").And.Contain("bus-name")
            .And.Contain("call hand_forward once");
    }

    internal static SpecDraft Draft(string id, string? yaml = null) =>
        new(id, $"goal of {id}", yaml ?? $"spec: {id}\ngoal: goal of {id}\n", []) { Repos = ["shop"] };

    private static string RecordWith(string id, IReadOnlyList<string> renamed) =>
        $"spec: {id}\ngoal: goal of {id}\n" + SpecOutcomeBlock.Render("run-0", "delivered", string.Empty,
            new SpecBuildNoteRead(new SpecBuildNote(id, renamed, [], [], []), false));

    private static PipelineContext Pipeline(IReadOnlyList<string> executed, string? firstYaml = null, bool withThird = false)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        List<DerivedSpec> specs =
        [
            new(Draft(First, firstYaml), "first", string.Empty, []),
            new(Draft(Second), "second", string.Empty, []),
        ];
        if (withThird) specs.Add(new DerivedSpec(Draft(Third), "third", string.Empty, []));
        pipeline.Set(ContextKeys.SpecSet,
            new SpecSet("t-7", specs, SpecAccounting.Empty, [], SpecSource.Derived) { Executed = executed });
        return pipeline;
    }

    private static IReadOnlyDictionary<string, SpecBuildNote> Notes(PipelineContext pipeline) =>
        pipeline.Get<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes);

    private static Dictionary<object, object> Outcome(string yaml)
    {
        var doc = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(yaml);
        return (Dictionary<object, object>)doc[SpecOutcomeBlock.Key];
    }
}
