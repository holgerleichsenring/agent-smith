using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-15-53d0: the operator's turns of a conversation, verbatim and oldest first, with author and time.</summary>
public sealed class ConversationOwnerWordsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Of_Transcript_KeepsOperatorTurnsOnlyAndFallsBackToTheConversationsUser()
    {
        var state = new ConversationState
        {
            JobId = "s-1", ChannelId = "C1", UserId = "U1", Platform = "dashboard", Project = "proj", TicketId = "",
            StartedAt = Noon,
            Transcript =
            [
                new TranscriptTurn(TranscriptRole.User, "Keep the limit at 50.", Noon, Author: "Sample Operator"),
                new TranscriptTurn(TranscriptRole.Assistant, "Proposal: 50.", Noon.AddMinutes(1)),
                new TranscriptTurn(TranscriptRole.User, "  ", Noon.AddMinutes(2)),
                new TranscriptTurn(TranscriptRole.User, "File them.", Noon.AddMinutes(3)),
            ],
        };

        var words = ConversationOwnerWords.Of(state);

        words.Select(w => (w.Author, w.At, w.Text)).Should().Equal(
            ("Sample Operator", Noon, "Keep the limit at 50."), ("U1", Noon.AddMinutes(3), "File them."));
    }

    // 2026-11-17-5701b: the e2e replay stored the filing click "approve" as an operator word.
    [Theory]
    [InlineData("prop-1")]
    [InlineData(null)]
    public void Of_ApprovalTurn_IsNotAnOwnerWord(string? proposalId)
    {
        var state = new ConversationState
        {
            JobId = "s-1", ChannelId = "C1", UserId = "U1", Platform = "dashboard", Project = "proj", TicketId = "",
            StartedAt = Noon,
            Transcript =
            [
                new TranscriptTurn(TranscriptRole.User, "approve", Noon, Decision: SpecDialogDecision.Approved,
                    DecisionProposalId: "prop-0", DecisionApplied: true),
                new TranscriptTurn(TranscriptRole.User, "Keep the limit at 50.", Noon.AddMinutes(1)),
                new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(2), Decision: SpecDialogDecision.Approved,
                    DecisionProposalId: "prop-1"),
            ],
        };

        var words = ConversationOwnerWords.Of(state, proposalId);

        words.Select(w => w.Text).Should().Equal("approve", "Keep the limit at 50.");
    }
}
