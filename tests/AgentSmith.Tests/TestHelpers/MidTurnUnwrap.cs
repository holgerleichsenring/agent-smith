using AgentSmith.Application.Services.Turns;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-11-18-c8e2a: every master's functions now hand on its sub-agents' notices, so the wrapper a test
/// asserts on sits one layer under the mid-turn one.</summary>
internal static class MidTurnUnwrap
{
    internal static AITool UnderMidTurn(this AITool tool) => tool is MidTurnMessageAIFunction m ? m.Inner : tool;
}
