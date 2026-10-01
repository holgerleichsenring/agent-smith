using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.ToolImages;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>2026-10-01-283dd: the words around a tool's image.</summary>
public sealed class ToolImageMessageComposerTests
{
    private static readonly DepositedToolImage Item =
        new(new ToolImage("image/png", ToolImageLoopFixture.Png(10, 10), "desktop\nrender"), "render_reference", "call_7");

    [Fact]
    public void Showing_CaptionsEachImageWithToolAndCall()
    {
        var message = new ToolImageMessageComposer().Showing([Item]);

        message.Role.Should().Be(ChatRole.User);
        message.Contents[0].Should().BeOfType<TextContent>()
            .Which.Text.Should().Be("[image from render_reference, call call_7: desktop render]");
        message.Contents[1].Should().BeOfType<DataContent>().Which.MediaType.Should().Be("image/png");
    }

    [Fact]
    public void NotShown_NamesTheImageAndTheReasonWithoutItsBytes()
    {
        var note = new ToolImageMessageComposer().NotShown(Item, "no vision");

        note.Should().Contain("not shown").And.Contain("no vision").And.Contain("64 bytes").And.Contain("desktop render");
        note.Should().NotContain(Convert.ToBase64String(Item.Image.Bytes));
    }
}
