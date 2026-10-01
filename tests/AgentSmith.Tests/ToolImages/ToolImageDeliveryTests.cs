using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-01-283dd: where the picture cannot arrive — a transport that drops it, a model that
/// cannot see — no image is sent, no user message follows the tool result, and the tool result
/// itself says an image exists and was not shown.
/// </summary>
public sealed class ToolImageDeliveryTests
{
    [Fact]
    public async Task ToolImage_CopilotBuilder_SendsNoImageAndTheNote()
    {
        new CopilotChatClientBuilder(new FakeCopilotRuntime(), NullLoggerFactory.Instance)
            .AcceptsImageAfterToolResult.Should().BeFalse();

        var last = await RunOneRender(acceptsImage: false, vision: true);

        AssertWithheld(last, ToolImageDelivery.ProviderReason);
    }

    [Fact]
    public async Task ToolImage_AgentWithoutVision_SendsNoImageAndTheNote()
    {
        var last = await RunOneRender(acceptsImage: true, vision: false);

        AssertWithheld(last, ToolImageDelivery.VisionReason);
    }

    [Theory]
    [InlineData(true, true, true, "")]
    [InlineData(false, true, false, ToolImageDelivery.ProviderReason)]
    [InlineData(true, false, false, ToolImageDelivery.VisionReason)]
    public void ToolImageDelivery_For_NeedsTheTransportAndTheVision(
        bool accepts, bool vision, bool delivered, string reason)
    {
        ToolImageDelivery.For(accepts, vision).Should().Be(new ToolImageDelivery(delivered, reason));
    }

    private static async Task<List<ChatMessage>> RunOneRender(bool acceptsImage, bool vision)
    {
        var fixture = new ToolImageLoopFixture();
        var chat = new ScriptedToolChat(["render"]);
        fixture.WithProvider("stub", chat, acceptsImage);
        await fixture.Loop("stub", vision).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            new ChatOptions { Tools = [fixture.DepositingTool("render", 1)] });
        return chat.Requests[^1];
    }

    private static void AssertWithheld(List<ChatMessage> request, string reason)
    {
        ToolImageLoopTests.Images(request).Should().BeEmpty();
        request[^1].Role.Should().Be(ChatRole.Tool, "a continuation of tool messages only stays one");
        var result = ToolImageLoopTests.ToolResults(request).Single();
        result.Should().StartWith("render done").And.Contain("an image exists and was not shown")
            .And.Contain(reason).And.Contain("image/png").And.Contain("render render 1");
    }
}
