using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>Records the master's tools and, when asked, spawns one child so its surface is seen.</summary>
internal sealed class SurfaceCapturingLoop(bool spawn = false) : IAgenticLoopRunner
{
    private const string OneTask = "[{\"name\":\"RepoScout\",\"activity\":\"read the sources\","
        + "\"task_description\":\"answer one question\",\"inherited_context\":"
        + "{\"pipeline_goal\":\"cut the spec\",\"prior_context_slice\":\"the transcript\"}}]";

    public IReadOnlyList<string> MasterTools { get; private set; } = [];

    public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
    {
        if (MasterTools.Count == 0)
        {
            MasterTools = [.. request.Tools!.OfType<AIFunction>().Select(t => t.Name)];
            if (spawn)
                await request.Tools!.OfType<AIFunction>().Single(t => t.Name == "spawn_agents").InvokeAsync(
                    new AIFunctionArguments { ["tasks"] = JsonDocument.Parse(OneTask).RootElement }, cancellationToken);
        }
        return new AgenticLoopResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            },
            TimeSpan.FromSeconds(1));
    }
}
