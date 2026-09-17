using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// The one rule for "this reply carries a draft", in both shapes the design master proposes
/// work in, and the per-platform rendering of a reply built on it.
/// </summary>
public sealed class SpecDialogDraftBlocksTests
{
    private const string PhaseDraft = "```yaml\nphase: p9999\ngoal: \"g\"\n```";
    private const string OutcomeDraft = "```outcome\nkind: bug\ntitle: \"t\"\n```";

    [Theory]
    [InlineData(PhaseDraft)]
    [InlineData(OutcomeDraft)]
    public void Contains_EitherDraftShape_IsFound(string draft) =>
        SpecDialogDraftBlocks.Contains("Proposal:\n" + draft).Should().BeTrue();

    [Fact]
    public void Contains_AnotherFencedBlock_IsNoDraft() =>
        SpecDialogDraftBlocks.Contains("Run this:\n```sql\nSELECT 1;\n```").Should().BeFalse();

    [Fact]
    public void Strip_AReplyWithADraft_KeepsTheProseAround()
    {
        var shown = SpecDialogDraftBlocks.Strip("Before.\n\n" + OutcomeDraft + "\n\nAfter.");

        shown.Should().Be("Before.\n\nAfter.");
    }

    [Fact]
    public void Strip_AReplyWithoutADraft_IsUnchanged()
    {
        const string reply = "  **Plain** answer with ```sql\nSELECT 1;\n``` in it.\n";

        SpecDialogDraftBlocks.Strip(reply).Should().Be(reply);
    }

    [Fact]
    public void Strip_ADraftOnlyReply_IsEmpty() =>
        SpecDialogDraftBlocks.Strip(PhaseDraft).Should().BeEmpty();

    [Fact]
    public void On_TheDashboard_ShowsTheReplyWithoutItsDraftAndKeepsItWhole()
    {
        var result = SpecDialogTurnResult.On("dashboard", "Here.\n" + PhaseDraft, new AnswerOutcome());

        result.Shown.Should().Be("Here.");
        result.Reply.Should().Be("Here.\n" + PhaseDraft);
    }

    [Theory]
    [InlineData("slack")]
    [InlineData("teams")]
    public void On_AChatPlatform_ShowsTheReplyUnchanged(string platform) =>
        SpecDialogTurnResult.On(platform, "Here.\n" + PhaseDraft, new AnswerOutcome())
            .Shown.Should().Be("Here.\n" + PhaseDraft);
}
