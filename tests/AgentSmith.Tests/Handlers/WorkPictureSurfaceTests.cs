using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-05-862ea: the design master states the work picture through state_work_picture — offered on every design
/// turn with a reply slot, never on the steward's surface; a refused picture comes back with the rule it broke.
/// </summary>
public sealed class WorkPictureSurfaceTests
{
    [Theory]
    [InlineData("Rebuild", "series")]
    [InlineData("rebuild", "Series")]
    public async Task StateWorkPicture_ValidValues_SetsTheSlot(string kind, string size)
    {
        var slot = new SpecDialogReplySlot();

        var answer = await Invoke(slot, kind, "the handover upload; repo-a is scaffolding", size);
        var corrected = await Invoke(slot, "feature", "repo-a", "one-spec");

        answer.Should().Be(WorkPictureToolHost.Stored);
        corrected.Should().Be("Stored. State it in one line of your reply.");
        slot.WorkPicture.Should().Be(new WorkPicture(UndertakingKind.Feature, "repo-a", UndertakingSize.OneSpec),
            "the last admitted call of a turn wins");
    }

    [Theory]
    [InlineData("migration", "repo-a", "series", "The kind 'migration' is none of: rebuild, feature, fix, document.")]
    [InlineData("1", "repo-a", "series", "The kind '1' is none of")]
    [InlineData("fix", "repo-a.\nAnd repo-b.", "series", "The truth is more than one line.")]
    [InlineData("fix", "repo-a", "huge", "The size 'huge' is none of: one-spec, series, plan.")]
    public async Task StateWorkPicture_UnknownKindOrMultiLineTruth_ReturnsTheRuleAndSetsNothing(
        string kind, string truth, string size, string rule)
    {
        var slot = new SpecDialogReplySlot();

        var answer = await Invoke(slot, kind, truth, size);

        slot.WorkPicture.Should().BeNull();
        answer.Should().StartWith("Not stored: ").And.Contain(rule);
    }

    [Fact]
    public void TicketActToolsFactory_DesignTurn_OffersStateWorkPicture()
    {
        var factory = new TicketActToolsFactory(MasterHandlerFixture.Verification());
        var seeded = Context();
        seeded.Pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());

        factory.Tools(seeded).Select(t => t.Name).Should().Contain(WorkPictureToolHost.Name);
        factory.Tools(Context()).Select(t => t.Name).Should().NotContain(WorkPictureToolHost.Name,
            "a run with no reply slot has nowhere to put the picture");
    }

    [Fact]
    public void TicketStewardTools_StewardSurface_DropsStateWorkPicture()
    {
        var slot = new SpecDialogReplySlot();
        IList<AITool> master = [.. new WorkPictureToolHost(slot).GetTools(null, null),
            .. new NameConversationToolHost(slot).GetTools(null, null)];

        TicketStewardTools.Of(master).Select(t => t.Name).Should()
            .NotContain(WorkPictureToolHost.Name).And.Contain(NameConversationToolHost.Name);
    }

    private static async Task<string> Invoke(SpecDialogReplySlot slot, string kind, string truth, string size) =>
        (await new WorkPictureToolHost(slot).GetTools(null, null).Single().InvokeAsync(
            new AIFunctionArguments { ["kind"] = kind, ["truth"] = truth, ["size"] = size }))!.ToString()!;

    private static AgenticMasterContext Context() => new(
        "design-partner-master", new Repository(new BranchName("main"), "/tmp"), string.Empty,
        new AgentConfig { Type = "claude" }, new PipelineContext());
}
