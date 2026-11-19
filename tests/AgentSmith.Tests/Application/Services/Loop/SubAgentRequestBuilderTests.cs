using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Events;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SubAgents;

/// <summary>2026-11-19-687eb: a child's tool call is a run event attributed to that child, before the call runs.</summary>
public sealed class SubAgentRequestBuilderTests
{
    [Fact]
    public async Task GrantedTool_WhenTheChildCallsIt_PublishesASubAgentToolCallUnderItsId()
    {
        var publisher = new RecordingEventPublisher();
        var ran = false;
        var readFile = AIFunctionFactory.Create((string path) => { ran = true; return $"read {path}"; }, "read_file");
        var context = SubAgentRunnerTests.BuildContext() with { ChildTools = [readFile] };
        var member = new SubAgentMember("sa-7", new SubAgentSpec(
            "PoC analyst", "map the capabilities", "task", new InheritedContext("goal", "slice")));

        var request = new SubAgentRequestBuilder(publisher).Build(member, context);
        var tool = request.Tools.OfType<AIFunction>().Single();
        await tool.InvokeAsync(new AIFunctionArguments { ["path"] = "src/a.cs" }, CancellationToken.None);

        ran.Should().BeTrue();
        tool.Name.Should().Be("read_file");
        var call = publisher.Events.OfType<SubAgentToolCallEvent>().Should().ContainSingle().Subject;
        call.RunId.Should().Be("run-test");
        call.SubAgentId.Should().Be("sa-7");
        call.ToolName.Should().Be("read_file");
        call.ArgsSummary.Should().Contain("src/a.cs");
    }
}
