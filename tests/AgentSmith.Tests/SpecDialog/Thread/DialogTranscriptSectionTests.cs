using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.TranscriptDigest.DigestTurns;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class DialogTranscriptSectionTests
{
    private static readonly IReadOnlyList<SpecDialogTurn> Transcript =
    [
        new(SpecDialogTurn.UserRole, "need a widget"),
        new(SpecDialogTurn.AssistantRole, "which ledger?"),
        new(SpecDialogTurn.UserRole, "the main one", Author: "Ada"),
    ];

    [Fact]
    public void DialogTranscriptSection_SeededThread_RendersOnlyUnseenTurnsUnderTheNote()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogThread, new DialogThread(false, [], [Transcript[2]]));

        var rendered = DialogTranscriptSection.Render(pipeline, Transcript, stewarded: false);

        rendered.Should().StartWith("[Your earlier turns — what you read, called and answered — are the messages before this one.")
            .And.EndWith("[user: Ada] the main one")
            .And.NotContain("need a widget").And.NotContain("which ledger?");
    }

    [Fact]
    public void DialogTranscriptSection_NoThread_RendersTheWindowAsBefore()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogThread, new DialogThread(true, [], [Transcript[2]]));

        DialogTranscriptSection.Render(new PipelineContext(), Transcript, stewarded: false)
            .Should().Be(RenderWindow(Transcript));
        DialogTranscriptSection.Render(pipeline, Transcript, stewarded: false)
            .Should().Be(RenderWindow(Transcript), "the steward's thread is not the design partner's");
    }
}
