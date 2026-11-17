using System.Text.Json;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Turns;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>2026-11-17-4860b: a code run's master is handed what its ticket's conversation addressed to it once — across
/// a second master (the next spec) and a checkpoint's JSON round trip — and reads the transcript only on a change.</summary>
public sealed class TicketRunMessagesTests
{
    private sealed class FakeConversation : ITicketConversationMessages
    {
        public DateTimeOffset? Activity { get; set; } = DateTimeOffset.UtcNow;
        public List<RunAddressedMessage> Messages { get; } = [];
        public int Reads { get; private set; }

        public Task<DateTimeOffset?> LastActivityAsync(string project, string ticketId, CancellationToken ct) =>
            Task.FromResult(Activity);

        public Task<IReadOnlyList<RunAddressedMessage>> AddressedToAsync(
            string project, string ticketId, string runId, int afterIndex, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<RunAddressedMessage>>([.. Messages.Where(m => m.Index > afterIndex)]);
        }
    }

    [Fact]
    public async Task DrainAsync_AMessage_IsHandedOverOnceAcrossMasters()
    {
        var conversation = new FakeConversation();
        conversation.Messages.Add(new(3, "keep the header", DateTimeOffset.UtcNow));
        var pipeline = new PipelineContext();

        var first = await For(conversation, pipeline).DrainAsync(default);
        conversation.Activity = conversation.Activity!.Value.AddSeconds(1);
        var second = await For(conversation, pipeline).DrainAsync(default);

        first.Should().ContainSingle().Which.Text.Should().Be("keep the header");
        second.Should().BeEmpty("the next spec's master starts past what the run was handed");
    }

    [Fact]
    public async Task DrainAsync_NoChangeInTheConversation_ReadsNoTranscript()
    {
        var conversation = new FakeConversation();
        var messages = For(conversation, new PipelineContext());

        await messages.DrainAsync(default);
        await messages.DrainAsync(default);

        conversation.Reads.Should().Be(1);
    }

    [Fact]
    public void Through_ACursorBackFromACheckpoint_IsRead()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketMessagesThrough, JsonSerializer.SerializeToElement(5));

        TicketRunMessages.Through(pipeline).Should().Be(5);
        TicketRunMessages.Through(new PipelineContext()).Should().Be(-1);
    }

    private static TicketRunMessages For(ITicketConversationMessages conversation, PipelineContext pipeline) =>
        new(conversation, pipeline, "sample", "7", "run-1");
}
