using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283df: the parts around the carry — the prompt names each carried set by name, id and
/// directory; a resumed run re-carries them into its fresh sandboxes; the studio toggle round-trips.
/// </summary>
public sealed class ReferenceRunCarryPartsTests
{
    private static readonly CarriedReferenceSet Carried =
        new("set-a", "site", "reference:site", "sample-api", ".agentsmith/reference/set-a", "s-1", 3);

    [Theory]
    [InlineData(false, "`.agentsmith/reference/set-a/`")]
    [InlineData(true, "`sample-api/.agentsmith/reference/set-a/`")]
    public void ReferencePromptSection_Carried_NamesEachSetByNameIdAndDirectory(bool prefixed, string path)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, [Carried]);

        var section = ReferencePromptSection.Carried(pipeline, prefixed);

        section.Should().Contain("site (set set-a, 3 files)").And.Contain(path)
            .And.Contain("`reference:site`").And.Contain("NOT part of the commit");
    }

    [Fact]
    public void ReferencePromptSection_Carried_RunCarryingNone_IsEmpty() =>
        ReferencePromptSection.Carried(new PipelineContext(), prefixed: false).Should().BeEmpty();

    [Fact]
    public async Task DialogueCheckpoint_AfterTheCarry_ReCarriesItBeforeTheCursor()
    {
        RunEvent? published = null;
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => published = e).Returns(Task.CompletedTask);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.TicketId, new TicketId("7"));
        pipeline.Set<IReadOnlyList<PipelineCommand>>(ContextKeys.RemainingCommands, [PipelineCommand.Simple(CommandNames.AgenticMaster)]);
        pipeline.Set(ContextKeys.ExecutionTrail, new List<ExecutionTrailEntry>
        {
            Ran(CommandNames.CheckoutSource), Ran(CommandNames.MaterializeReferenceSets),
        });

        await new DialogueCheckpointWriter(events.Object, Mock.Of<AgentSmith.Contracts.Services.IPipelineContextSerializer>(),
                NullLogger<DialogueCheckpointWriter>.Instance)
            .TryCheckpointAsync(pipeline, new DialogQuestion("q", QuestionType.FreeText, "?", null, null, null, TimeSpan.FromHours(1)),
                "dialogue-1", CancellationToken.None);

        var commands = JsonSerializer.Deserialize<List<CheckpointCommand>>(((RunCheckpointedEvent)published!).RemainingCommandsJson)!;
        commands.Select(c => c.Name).Should().Equal(
            CommandNames.CheckoutSource, CommandNames.MaterializeReferenceSets, CommandNames.AgenticMaster);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProjectSandbox_BrowserToggle_RoundTripsThroughTheStudio(bool enabled)
    {
        var entity = new ProjectEntity("proj", "gpt5", "azdo", ["api"], null, [],
            Sandbox: new ProjectSandbox(BrowserEnabled: enabled));

        var stored = RawProjectPatch.Apply(entity, existing: null);
        var projected = ProjectEntityMapping.ToProject("proj", stored);

        (stored.Sandbox!.Browser is null).Should().Be(!enabled, "off stores no browser block at all");
        (projected.Sandbox!.BrowserEnabled == true).Should().Be(enabled);
    }

    private static ExecutionTrailEntry Ran(string command) =>
        new(command, null, true, "ok", DateTimeOffset.UnixEpoch, TimeSpan.Zero, null);
}
