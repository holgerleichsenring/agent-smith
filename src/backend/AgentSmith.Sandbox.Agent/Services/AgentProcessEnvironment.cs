using System.Diagnostics;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// 2026-10-02-35b2: what a step's process does NOT inherit from the agent. The
/// agent needs the step bus's address and its job id; a command it runs — a build, a test, a shell
/// the model wrote — needs neither, and an inherited REDIS_URL is the bus every sandbox shares. A
/// step that sets one of them itself keeps its own value. This hides the values from env and ps;
/// it does not make them unreachable (2026-10-02-f35f).
/// </summary>
internal static class AgentProcessEnvironment
{
    internal static readonly string[] AgentOnly = ["REDIS_URL", "JOB_ID"];

    internal static void Scrub(ProcessStartInfo info, IReadOnlyDictionary<string, string>? stepEnv)
    {
        ArgumentNullException.ThrowIfNull(info);
        foreach (var key in AgentOnly)
            if (stepEnv?.ContainsKey(key) != true) info.Environment.Remove(key);
    }
}
