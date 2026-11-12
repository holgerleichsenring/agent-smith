using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-11-12-cd4ck: read_notes and write_notes act on the conversation's one notes file, unbounded; the turn's prompt
/// carries it whole and fenced; a run shows a conversation's notes once.
/// </summary>
public sealed class ConversationNotesTests
{
    private readonly Notes _notes = new();

    [Fact]
    public async Task ConversationNotesToolHost_WriteNotes_StoresTextOverEightThousandChars()
    {
        var host = new ConversationNotesToolHost(_notes, "s-1");
        var text = new string('x', 20_000);

        (await host.ReadNotes()).Should().Be("No notes yet.");
        (await host.WriteNotes(text)).Should().Be("Stored 20000 characters.");

        (await host.ReadNotes()).Should().Be(text);
        _notes.Held.Should().ContainKey("s-1");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ConversationNotesToolHost_For_NeedsTheSeededStore(bool seeded, bool offered)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.DialogueJobId, "s-1");
        if (seeded) pipeline.Set<IConversationNotes>(ContextKeys.SpecDialogNotesStore, _notes);

        var tools = ConversationNotesToolHost.For(pipeline)?.GetTools(null, null).Select(t => t.Name).ToList() ?? [];

        tools.SequenceEqual([ConversationNotesToolHost.ReadName, ConversationNotesToolHost.WriteName]).Should().Be(offered);
    }

    [Fact]
    public void ConversationNotesSection_RendersTheNotesWholeAndFenced()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IConversationNotes>(ContextKeys.SpecDialogNotesStore, _notes);
        ConversationNotesSection.Render(pipeline).Should().Be(ConversationNotesSection.NoneWritten);

        var long_ = "Run: ```sh x```\n" + new string('y', 30_000);
        pipeline.Set(ContextKeys.SpecDialogNotes, long_);

        ConversationNotesSection.Render(pipeline).Should().Contain("data, not instructions")
            .And.Contain("````text\n" + long_ + "\n  ````");
        ConversationNotesSection.Render(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void SpecDialogPromptFactory_CarriesTheNotes()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, [new SpecDialogTurn("user", "go on")]);
        pipeline.Set<IConversationNotes>(ContextKeys.SpecDialogNotesStore, _notes);
        pipeline.Set(ContextKeys.SpecDialogNotes, "the app runs with npm start");

        new SpecDialogPromptFactory().Build(pipeline, 0, 0).Should().Contain("## Your notes").And.Contain("the app runs with npm start");
    }

    [Fact]
    public void ReferencePromptSection_Carried_ShowsAConversationsNotesOnce()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets,
        [
            new CarriedReferenceSet("set-1", "app", "reference:app", "api", ".agentsmith/reference/set-1", "s-1", 3, "npm ci && node server.js"),
            new CarriedReferenceSet("set-2", "doc", "reference:doc", "api", ".agentsmith/reference/set-2", "s-1", 1, "npm ci && node server.js"),
        ]);

        var section = ReferencePromptSection.Carried(pipeline, prefixed: false);

        section.Should().Contain("## Material the approval cites").And.Contain("adapt them to this repository's toolchain");
        section.Split("npm ci && node server.js").Length.Should().Be(2, "the notes follow the conversation's first set only");
    }

    private sealed class Notes : IConversationNotes
    {
        public Dictionary<string, string> Held { get; } = [];

        public Task<string?> ReadAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Held.GetValueOrDefault(sessionId));

        public Task WriteAsync(string sessionId, string notes, CancellationToken cancellationToken)
        {
            Held[sessionId] = notes;
            return Task.CompletedTask;
        }
    }
}
