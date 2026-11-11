using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-11-205fj: what spawn_agents says about itself, per surface. A design turn's children read only and its fan-out
/// is bounded by numbers the gate enforces — the turn's own budget and the child's iteration ceiling — so the design text
/// renders those numbers rather than a literal, and names none of a coding run's progress, verdict or gate tools.
/// </summary>
internal static class SpawnAgentsDescription
{
    private const string Opening =
        "Runs sub-agents in parallel, each in a fresh context, and returns when all have finished. ";

    private const string Task =
        "Each task needs a descriptive name (a role, not 'agent1' or 'worker' — generic names are refused), a one-line "
        + "activity, the task description, and the inherited context: the goal and the slice of what you know that the "
        + "child needs, because it sees nothing else of your conversation. ";

    private const string Result =
        "The result lists per child its status, id, tool calls, cost and its whole answer; a child is told what a good "
        + "answer holds, and only a runaway past " + SubAgentAnswer.CeilingText + " characters is cut, saying so first.";

    /// <summary>A run's surface — the text p0177 and 0620e shipped, unchanged.</summary>
    internal const string Run = Opening + Task
        + "A child works in your checkouts with the base file and command tools — only the read tools in a design "
        + "conversation — and none of your run's progress, verdict, gate or spawn tools. " + Result;

    /// <summary>A design turn's surface: how many children it may start and how far each may go, as configured.</summary>
    internal static string Design(int children, int rounds) => Opening
        + $"This turn may start up to {children} children in all, each for up to {rounds} tool rounds. " + Task
        + "A child reads your checkouts with the read tools and changes nothing. " + Result;
}
