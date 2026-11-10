using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Prompts;

/// <summary>2026-11-09-0620a: a request becomes the tool list as one block and each message as one block.</summary>
public sealed class PromptBlocksTests
{
    [Fact]
    public void PromptBlocks_ToolLoopCall_RendersToolsAndEachMessageAsOneBlock()
    {
        var tool = AIFunctionFactory.Create((string path) => path, "read_file", "Reads one file of the repository.");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a design partner."),
            new(ChatRole.User, "Read the readme."),
            new(ChatRole.Assistant, [new FunctionCallContent("c1", "read_file", new Dictionary<string, object?> { ["path"] = "README.md" })]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", "# Sample app")]),
        };

        var blocks = PromptBlocks.From(messages, new ChatOptions { Tools = [tool] });

        blocks.Should().HaveCount(5);
        blocks[0].Kind.Should().Be(PromptBlockKind.Tools);
        blocks[0].Content.Should().Contain("## read_file").And.Contain("Reads one file of the repository.").And.Contain("path");
        blocks[1].Role.Should().Be("system");
        blocks[3].Content.Should().Contain("[tool call read_file #c1]").And.Contain("README.md");
        blocks[4].Content.Should().Contain("[tool result #c1]").And.Contain("# Sample app");
    }

    [Fact]
    public void PromptBlocks_SameMessageTwice_RendersTheSameText()
    {
        var message = new ChatMessage(ChatRole.User, "same");

        PromptBlocks.From([message], null).Single().Should().Be(PromptBlocks.From([message], null).Single());
    }

    [Fact]
    public void PromptBlocks_Image_IsRenderedByItsIdentityNotItsBytes()
    {
        var image = new DataContent(new byte[] { 1, 2, 3 }, "image/png");

        var block = PromptBlocks.From([new ChatMessage(ChatRole.User, [image])], null).Single();

        block.Content.Should().StartWith("[image/png, 3 bytes, sha256 ");
    }
}
