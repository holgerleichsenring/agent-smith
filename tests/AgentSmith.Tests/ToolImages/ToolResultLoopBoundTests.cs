using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-07-6b9da: through the REAL factory chain, every tool result is bounded before it
/// enters the loop's history, and reaches the provider as plain text — a JSON-string result
/// would be escaped by the provider to about twice its size.
/// </summary>
public sealed class ToolResultLoopBoundTests
{
    [Fact]
    public async Task ToolLoop_ReadFileReturning1MB_ModelReceivesBoundedPlainString()
    {
        var file = "HEAD" + new string('l', 1_000_000 - 8) + "TAIL";

        var result = await ResultReachingProvider(AIFunctionFactory.Create(() => file, "read_file"));

        var text = result.Should().BeOfType<string>().Subject;
        text.Length.Should().BeLessThanOrEqualTo(100_000);
        text.Should().StartWith("HEAD").And.EndWith("TAIL").And.Contain("of 1,000,000 characters");
    }

    [Fact]
    public async Task Factory_JsonStringResult_ReachesInnerClientAsString()
    {
        const string content = "line \"one\"\n\tline two";

        var result = await ResultReachingProvider(AIFunctionFactory.Create(() => content, "read_file"));

        result.Should().BeOfType<string>().Which.Should().Be(content);
    }

    private static async Task<object?> ResultReachingProvider(AIFunction tool)
    {
        var fixture = new ToolImageLoopFixture();
        var chat = new ScriptedToolChat([tool.Name]);
        fixture.WithProvider("stub", chat);

        await fixture.Loop("stub").GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], new ChatOptions { Tools = [tool] });

        return chat.Requests[^1].SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result;
    }
}
