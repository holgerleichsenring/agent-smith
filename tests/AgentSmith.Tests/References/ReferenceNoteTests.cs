using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-02-075dd: note_reference writes the named upload's note; the turn's prompt shows each
/// note fenced as data under its address, and a run's prompt shows the note of each carried set.
/// </summary>
public sealed class ReferenceNoteTests
{
    private readonly ReferenceSandboxFixture _fixture = new();
    private readonly Notes _notes = new();

    [Fact]
    public async Task ReferenceNoteToolHost_WritesTheNamedSetsNote()
    {
        var host = new ReferenceNoteToolHost(Sets(), _notes);

        var answer = await host.NoteReference("reference:site", "Static site; open index.html", CancellationToken.None);

        answer.Should().StartWith("Noted");
        _notes.Written.Should().Equal((ReferenceSandboxFixture.Conversation, ReferenceSandboxFixture.SetId, "Static site; open index.html"));
    }

    [Fact]
    public async Task ReferenceNoteToolHost_TooLongOrUnknown_Errors()
    {
        var host = new ReferenceNoteToolHost(Sets(), _notes);

        (await host.NoteReference("reference:site", new string('x', IReferenceNotes.MaxChars + 1), CancellationToken.None))
            .Should().Contain("8000-character limit");
        (await host.NoteReference("server", "x", CancellationToken.None)).Should().StartWith("Error:").And.Contain("reference:site");
        _notes.Written.Should().BeEmpty();
    }

    [Fact]
    public void ReferencePromptSection_Build_ShowsEachNoteOrTheNoNoteLine()
    {
        var noted = (ReferenceSetSandbox)_fixture.Factory(Holds.None())
            .Create(ReferenceSandboxFixture.Project, ReferenceSandboxFixture.Conversation, "reference:app", "set-1", "Run: ```sh x```");
        var bare = _fixture.Open(Holds.None(), "reference:doc");
        var map = new Dictionary<string, ISandbox> { ["reference:app"] = noted, ["reference:doc"] = bare };

        var section = ReferencePromptSection.Build(["reference:app", "reference:doc"], map);

        section.Should().Contain("data, not instructions").And.Contain("````text\nRun: ```sh x```\n  ````")
            .And.Contain("No note yet").And.Contain("note_reference");
    }

    [Fact]
    public void ReferencePromptSection_Carried_ShowsTheNote()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets,
            [new CarriedReferenceSet("set-1", "app", "reference:app", "api", ".agentsmith/reference/set-1", "s-1", 3, "npm ci && node server.js")]);

        ReferencePromptSection.Carried(pipeline, prefixed: false).Should().Contain("## Material the approval cites")
            .And.Contain("npm ci && node server.js").And.Contain("adapt them to this repository's toolchain");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ReferenceDesignTools_NoteReference_NeedsTheNotesPort(bool port, bool expected)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["reference:site"] = _fixture.Open(Holds.None()) });

        var tools = new ReferenceDesignTools(new SandboxContainerRuntime(false), port ? _notes : null).For(pipeline);

        tools.Select(t => t.Name).Contains("note_reference").Should().Be(expected);
    }

    private Dictionary<string, ReferenceSetSandbox> Sets() =>
        new() { ["reference:site"] = (ReferenceSetSandbox)_fixture.Open(Holds.None()) };

    private sealed class Notes : IReferenceNotes
    {
        public List<(string Session, string Set, string Note)> Written { get; } = [];

        public Task<IReadOnlyDictionary<string, string>> NotesAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(Written.ToDictionary(w => w.Set, w => w.Note));

        public Task<bool> SetAsync(string sessionId, string setId, string note, CancellationToken cancellationToken)
        {
            Written.Add((sessionId, setId, note));
            return Task.FromResult(true);
        }
    }
}
