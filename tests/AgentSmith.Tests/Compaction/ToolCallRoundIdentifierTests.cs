using AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;
using FluentAssertions;
using OpenAI.Chat;

namespace AgentSmith.Tests.Compaction;

public sealed class ToolCallRoundIdentifierTests
{
    [Fact]
    public void FindTailStartIndex_NoMessages_ReturnsZero()
    {
        ToolCallRoundIdentifier.FindTailStartIndex(Array.Empty<ChatMessage>(), 2)
            .Should().Be(0);
    }

    [Fact]
    public void FindTailStartIndex_NRoundsZero_ReturnsCount()
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("sys"),
            new UserChatMessage("hi")
        };
        ToolCallRoundIdentifier.FindTailStartIndex(messages, 0)
            .Should().Be(messages.Count);
    }

    [Fact]
    public void FindTailStartIndex_BareAssistantReply_CountsAsOneRound()
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("sys"),
            new UserChatMessage("hi"),
            new AssistantChatMessage("plain reply")
        };

        var idx = ToolCallRoundIdentifier.FindTailStartIndex(messages, 1);

        // The assistant reply is the only round; tail starts at index 2.
        idx.Should().Be(2);
    }

    [Fact]
    public void FindTailStartIndex_TwoCompleteToolRounds_BoundaryRespectsPair()
    {
        var fakeToolCall1 = ChatToolCall.CreateFunctionToolCall("call_1", "list_files", BinaryData.FromString("{}"));
        var fakeToolCall2 = ChatToolCall.CreateFunctionToolCall("call_2", "read_file", BinaryData.FromString("{}"));

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("sys"),
            new UserChatMessage("analyze"),
            new AssistantChatMessage(new[] { fakeToolCall1 }),
            new ToolChatMessage("call_1", "result_1"),
            new AssistantChatMessage(new[] { fakeToolCall2 }),
            new ToolChatMessage("call_2", "result_2"),
            new AssistantChatMessage("done")
        };

        var idxKeep2 = ToolCallRoundIdentifier.FindTailStartIndex(messages, 2);

        // 2 rounds at tail = "done" (round 1) + read_file pair (round 2).
        // Tail starts at the AssistantChatMessage with read_file (index 4).
        idxKeep2.Should().Be(4);
    }

    [Fact]
    public void FindTailStartIndex_AllMessagesFitInN_ReturnsFirstAssistantRoundIndex()
    {
        var fakeToolCall = ChatToolCall.CreateFunctionToolCall("call_x", "f", BinaryData.FromString("{}"));
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("sys"),
            new UserChatMessage("u"),
            new AssistantChatMessage(new[] { fakeToolCall }),
            new ToolChatMessage("call_x", "r")
        };

        var idx = ToolCallRoundIdentifier.FindTailStartIndex(messages, 5);

        // Only 1 complete round exists; identifier returns its boundary.
        idx.Should().Be(2);
    }

    [Fact]
    public void FindTailStartIndex_ToolMessageWithoutAssistant_StopsAtUserBoundary()
    {
        // Pathological: tool messages without preceding assistant. Identifier should not crash;
        // it gives up when a non-Assistant non-Tool message blocks the walk.
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("sys"),
            new UserChatMessage("u"),
            new ToolChatMessage("orphan_call", "weird")
        };

        var idx = ToolCallRoundIdentifier.FindTailStartIndex(messages, 1);

        // Walking backward: skip ToolChatMessage(orphan_call), hit UserChatMessage —
        // not an assistant, can't form a round. idx points after walked tool messages.
        idx.Should().Be(2);
    }
}
