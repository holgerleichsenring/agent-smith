using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-01-283dd: a tool's image enters the loop history ONCE, as a user message right
/// after the tool message, and stays out of any loop but the one whose tool deposited it.
/// </summary>
public sealed class ToolImageLoopTests
{
    [Fact]
    public async Task ToolImage_DepositedOnIterationOne_AppearsOnceInEveryLaterRequestDirectlyAfterTheToolMessage()
    {
        var fixture = new ToolImageLoopFixture();
        var chat = new ScriptedToolChat(["render"], ["check"]);
        fixture.WithProvider("stub", chat);

        await fixture.Loop("stub").GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            new ChatOptions { Tools = [fixture.DepositingTool("render", 1), fixture.DepositingTool("check", 0)] });

        chat.Requests.Should().HaveCount(3);
        Images(chat.Requests[0]).Should().BeEmpty();
        foreach (var request in chat.Requests.Skip(1))
        {
            Images(request).Should().ContainSingle("the image is in the history once, not re-sent per iteration");
            var toolIndex = request.FindIndex(m => m.Role == ChatRole.Tool);
            var imageMessage = request[toolIndex + 1];
            imageMessage.Role.Should().Be(ChatRole.User);
            imageMessage.Contents.OfType<DataContent>().Should().ContainSingle();
            imageMessage.Text.Should().Contain("render").And.Contain("call_0_0");
        }
    }

    [Fact]
    public async Task ToolImage_SixInOneLoop_SendsFourAndNamesTwo()
    {
        var fixture = new ToolImageLoopFixture();
        var chat = new ScriptedToolChat(["a", "b", "c"]);
        fixture.WithProvider("stub", chat);

        await fixture.Loop("stub").GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            new ChatOptions
            {
                Tools = [fixture.DepositingTool("a", 2), fixture.DepositingTool("b", 2), fixture.DepositingTool("c", 2)],
            });

        var last = chat.Requests[^1];
        Images(last).Should().HaveCount(4);
        ToolResults(last).Count(r => r.Contains("was not shown")).Should().Be(1, "both withheld images are call c's");
        ToolResults(last).Single(r => r.StartsWith("c done")).Split("was not shown").Should().HaveCount(3);
    }

    [Fact]
    public async Task ToolImage_SubAgentDeposit_DoesNotReachTheParentLoop()
    {
        var fixture = new ToolImageLoopFixture();
        var parent = new ScriptedToolChat(["spawn"]);
        var child = new ScriptedToolChat(["render"]);
        fixture.WithProvider("parent", parent).WithProvider("child", child);
        var render = fixture.DepositingTool("render", 1);
        var spawn = AIFunctionFactory.Create(async () =>
        {
            var answer = await fixture.Loop("child").GetResponseAsync(
                [new ChatMessage(ChatRole.User, "child task")], new ChatOptions { Tools = [render] });
            return answer.Text;
        }, "spawn");

        await fixture.Loop("parent").GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")], new ChatOptions { Tools = [spawn] });

        Images(child.Requests[^1]).Should().ContainSingle("the child's own loop shows its tool's image");
        parent.Requests.Should().AllSatisfy(r => Images(r).Should().BeEmpty());
    }

    internal static List<DataContent> Images(IEnumerable<ChatMessage> request) =>
        [.. request.SelectMany(m => m.Contents).OfType<DataContent>()];

    internal static List<string> ToolResults(IEnumerable<ChatMessage> request) =>
        [.. request.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? "")];
}
