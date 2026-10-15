using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.SpecDialog.TranscriptDigest.DigestTurns;

namespace AgentSmith.Tests.SpecDialog.TranscriptDigest;

/// <summary>2026-10-14-dbceo: the window bounds the design partner's and the ticket steward's prompt alike.</summary>
public sealed class TranscriptWindowPromptTests
{
    private static readonly IReadOnlyList<SpecDialogTurn> Long =
    [
        U("require the ledger widget", "Ada"), A(Big("FOLDED-1", 60_000)), U(Big("FOLDED-2", 60_000)),
        A(Big("FOLDED-3", 60_000)), Digest("DIGEST-OF-THE-START", 3), A("recent answer"), U("recent question", "Bob"),
    ];

    private static PipelineContext With(IReadOnlyList<SpecDialogTurn> transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogTranscript, transcript);
        pipeline.Set(ContextKeys.SpecDialogFrameworkFacts,
            new FrameworkFacts("agentsmith/42", "enqueued", "in-progress", "approved"));
        return pipeline;
    }

    [Fact]
    public void SpecDialogPromptFactory_LongConversationWithDigest_PromptIsUnderTheBoundPlusPinnedTurns()
    {
        var overhead = new SpecDialogPromptFactory().Build(With([U("x")]), 0, 0).Length;

        var prompt = new SpecDialogPromptFactory().Build(With(Long), 0, 0);

        prompt.Should().Contain(TranscriptWindow.Render(Long)).And.Contain("DIGEST-OF-THE-START").And.NotContain("FOLDED-");
        prompt.Length.Should().BeLessThan(overhead + TranscriptWindow.VerbatimChars);
    }

    [Fact]
    public void TicketStewardPromptFactory_LongConversationWithDigest_PromptCarriesTheWindowNotEveryTurn()
    {
        var prompt = new TicketStewardPromptFactory().Build(With(Long), imagesExisting: 0, imagesCarried: 0);

        prompt.Should().Contain("DIGEST-OF-THE-START").And.Contain("[user: Ada] require the ledger widget")
            .And.Contain("[user: Bob] recent question").And.NotContain("FOLDED-");
        prompt.Length.Should().BeLessThan(TranscriptWindow.VerbatimChars);
    }
}
