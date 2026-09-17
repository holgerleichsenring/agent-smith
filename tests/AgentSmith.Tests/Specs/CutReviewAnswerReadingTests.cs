using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-ffa7: the reviewer is taught ids written <c>[R3]</c>, so its answer is read past
/// brackets in its prose, and from the last turn that says anything.
/// </summary>
public sealed class CutReviewAnswerReadingTests
{
    private const string Finding = """
        [{"phase_id":"p1a","criterion":"every sender uses the new bus","problem":"uncheckable",
          "why":"no command shows it","cites":null}]
        """;

    [Fact]
    public async Task Review_ProseCitingABracketedId_StillReadsTheAnswer()
    {
        var review = await Review(new ChatMessage(ChatRole.Assistant,
            "Per [R1] the file is absent, see [R2].\n" + Finding + "\nThat is all [R1]."));

        review.Findings.Should().ContainSingle("a bracket in the prose is not the answer");
        review.Problem.Should().BeNull();
    }

    [Fact]
    public async Task Review_ProseCitingABracketedIdAndAnEmptyAnswer_IsCleanNotUnreadable()
    {
        var review = await Review(new ChatMessage(ChatRole.Assistant, "I checked [R1].\n[]"));

        review.Deliverable.Should().BeTrue();
        review.Problem.Should().BeNull("an empty array was answered, not an unreadable reply");
    }

    [Fact]
    public async Task Review_AnswerFollowedByAnEmptyTurn_IsStillRead()
    {
        var review = await Review(
            new ChatMessage(ChatRole.Assistant, Finding), new ChatMessage(ChatRole.Assistant, string.Empty));

        review.Findings.Should().ContainSingle();
    }

    private static Task<SpecCutReview> Review(params ChatMessage[] reply) =>
        Reviewer(new CappingFactory(new FixedReply(reply))).ReviewAsync(
            Set("every sender uses the new bus"), "the ticket", look: null,
            new AgentConfig(), Tracker(), CancellationToken.None);

    private sealed class FixedReply(ChatMessage[] reply) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new ChatResponse([.. reply]));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
