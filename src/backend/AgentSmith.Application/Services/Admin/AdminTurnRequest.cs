using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the loop request of an admin turn, under the dialog surface's limits — the agent's dialog
/// iteration ceiling, compaction against the dialog window, the capture of what it forwards, and the thread its
/// previous turn ended with as the messages it continues.
/// </summary>
public static class AdminTurnRequest
{
    public static AgenticLoopRequest Build(
        AgentConfig agent, string systemPrompt, string userPrompt, IList<AITool> tools,
        PipelineContext pipeline, SpecDialogReplySlot slot)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(slot);
        slot.Thread.Lead(stewarded: false);
        return new AgenticLoopRequest(agent, TaskType.Primary, systemPrompt, userPrompt, tools, Name: "admin-assistant")
        {
            MaxIterations = agent.DialogMasterLoopIterations,
            Compaction = agent.Compaction,
            ContextWindowTokensOverride = agent.DialogContextWindowTokens,
            ThreadCapture = slot.Thread,
            PriorMessages = DialogThreadSeed.For(pipeline, stewarded: false)?.Messages,
        };
    }
}
