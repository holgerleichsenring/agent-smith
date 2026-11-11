using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-11-11-2bdfa: an epic child's reviewer reads every sibling's goal and scope, so a capability a sibling takes
/// is not judged dropped or postponed by the child.
/// </summary>
public sealed class ProposalReviewSiblingsTests
{
    private const string Third = "2026-10-14-9000d";
    private const string Step = "Write the orders through the buffered file writer.";
    private const string NewStep = "Write the orders through the streaming file writer.";

    [Fact]
    public async Task ProposalReview_EpicChild_PromptListsEverySiblingButItself()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(EpicReply(Yaml(Parent, "The export is rebuilt in three slices."),
            [Yaml(First), Yaml(Second, "The import reads the export file.", requires: [First]), Yaml(Third, "The archive keeps the file.")]));

        await rig.RunAsync();

        var first = rig.Reviewed.First(i => i.Draft.SpecId == First);
        first.Siblings.Select(s => s.SpecId).Should().Equal(Second, Third);
        first.Siblings[0].Should().BeEquivalentTo(new
        {
            Goal = "The import reads the export file.", ScopeIn = "The export handler and its file writer.",
            ScopeOut = "The import side stays as it is.",
        });
        var prompt = SpecAttackPrompt.For(first, null);
        prompt.Should().Contain(SpecAttackSiblingsSection.Heading).And.Contain($"- {Second}: The import reads the export file.")
            .And.Contain($"- {Third}: The archive keeps the file.").And.NotContain($"- {First}:")
            .And.NotContain($"- {Parent}:", "the parent is not a sibling");
        rig.Reviewed.Where(i => i.Draft.SpecId == Third).Should()
            .OnlyContain(i => i.Siblings.Select(s => s.SpecId).SequenceEqual(new[] { First, Second }));
    }

    [Fact]
    public async Task ProposalReview_LoneSpec_PromptHasNoSiblingSection()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 1) };
        rig.Propose(SpecReply(Yaml(Lone)));

        await rig.RunAsync();

        rig.Reviewed.Should().NotBeEmpty().And.OnlyContain(i => i.Siblings.Count == 0);
        SpecAttackPrompt.For(rig.Reviewed.First(), null).Should().NotContain(SpecAttackSiblingsSection.Heading)
            .And.Contain(SpecAttackJudgement.Lone);
    }

    [Fact]
    public async Task ProposalReview_AfterRevision_NextRoundShowsRevisedSiblings()
    {
        var rig = new ProposalReviewRig { Settings = Of(rounds: 2) };
        rig.Propose(Epic());
        rig.OnReview = input => input.Round > 1 ? Build()
            : input.Draft.SpecId == First ? RefuseCuts(Cut(SpecRegisterNames.Steps, Step, NewStep))
            : Refuse("the import is not taken by any slice");
        rig.Replies(_ => Epic(Yaml(First, step: NewStep), Yaml(Second, "The import reads the export file.", requires: [First])));

        await rig.RunAsync();

        rig.Reviewed.Where(i => i.Draft.SpecId == First && i.Round == 1).Should()
            .OnlyContain(i => i.Siblings.Single().Goal == "The export writes every order of the day into one file.");
        rig.Reviewed.Where(i => i.Draft.SpecId == First && i.Round == 2).Should().NotBeEmpty()
            .And.OnlyContain(i => i.Siblings.Single().Goal == "The import reads the export file.");
    }
}
