using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-11-2bdfc: the open questions published with a design turn's proposal are the ones stated with the drafts it
/// shows; a re-proposal that is not used takes its questions with it.
/// </summary>
public sealed class ProposalReviewQuestionsFollowTests
{
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";
    private static readonly OpenQuestion Earlier = Question("Does the export keep cancelled orders?", "Leave them out.", Lone);
    private static readonly OpenQuestion Later = Question("Who owns the file?", "Accounting.", Lone);

    private static ProposalReviewRig Lonely(int rounds = 1)
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds), OnReview = _ => Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) };
        rig.Slot.Questions = [Earlier];
        rig.Propose(SpecReply(Yaml(Lone)));
        return rig;
    }

    [Fact]
    public async Task ProposalReview_ReplyNotUsed_QuestionsAreTheFirstReplys()
    {
        var rig = Lonely();
        rig.Replies(rig.Asks(_ => "not a proposal at all", Later));

        await rig.RunAsync();

        rig.Slot.Questions.Should().Equal(Earlier);
        rig.Pipeline.Set(ContextKeys.MasterAnswer, "Here is the spec.");
        (await new CollectSpecDialogReplyHandler().ExecuteAsync(new CollectSpecDialogReplyContext(rig.Pipeline),
            CancellationToken.None)).IsSuccess.Should().BeTrue();
        rig.Slot.Outcome!.Questions.Should().Equal([Earlier], "the collect step lays the restored questions on the outcome");
    }

    [Fact]
    public async Task ProposalReview_ReplyAllReverted_QuestionsAreTheFirstReplys()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Slot.Questions = [Earlier];
        rig.OnReview = input => input.Draft.SpecId == First ? Cuts(Cut(SpecRegisterNames.Steps, Step, NewStep)) : Build();
        rig.Propose(Epic());
        rig.Replies(rig.Asks(_ => Epic(Yaml(First), Yaml(Second, "A goal nobody cut.", requires: [First])), Later));

        await rig.RunAsync();

        rig.DraftOf(Second).Goal.Should().NotBe("A goal nobody cut.");
        rig.Slot.Questions.Should().Equal(Earlier);
    }

    [Fact]
    public async Task ProposalReview_ReplyUsed_QuestionsAreTheRevisions()
    {
        var rig = Lonely();
        rig.Replies(rig.Asks(_ => SpecReply(Yaml(Lone, step: NewStep)), Later));

        await rig.RunAsync();

        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
        rig.Slot.Questions.Should().Equal(Later);
    }

    [Fact]
    public async Task ProposalReview_ReplyUsedWithoutQuestions_EarlierQuestionsStand()
    {
        var rig = Lonely();
        rig.Replies(rig.Asks(_ => SpecReply(Yaml(Lone, step: NewStep))));

        await rig.RunAsync();

        rig.DraftOf(Lone).Yaml.Should().Contain(NewStep);
        rig.Slot.Questions.Should().Equal([Earlier], "a reply that stated none keeps the earlier list");
    }

    [Fact]
    public async Task ProposalReview_ReplyChangedNoDraft_ItsQuestionsStand()
    {
        var rig = Lonely();
        rig.Replies(rig.Asks(_ => SpecReply(Yaml(Lone), "Nothing to change."), Later));

        await rig.RunAsync();

        rig.Slot.Questions.Should().Equal([Later], "the drafts shown are the ones it re-proposed");
    }
}
