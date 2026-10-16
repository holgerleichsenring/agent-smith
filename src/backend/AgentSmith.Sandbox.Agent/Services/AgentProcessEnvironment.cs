using System.Diagnostics;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// 2026-10-02-35b2: what a step's process does NOT inherit from the agent. The
/// agent needs the step bus's address and its job id; a command it runs — a build, a test, a shell
/// the model wrote — needs neither, and an inherited REDIS_URL is the bus every sandbox shares. A
/// step that sets one of them itself keeps its own value. This hides the values from env and ps;
/// it does not make them unreachable (2026-10-02-f35f).
/// 2026-10-16-8648: on a musl image the launcher puts the agent's own libstdc++ directory on
/// LD_LIBRARY_PATH; a step's tools must load the image's libraries, so that entry goes too.
/// </summary>
internal static class AgentProcessEnvironment
{
    internal static readonly string[] AgentOnly = ["REDIS_URL", "JOB_ID"];

    /// <summary>The launcher names the directory it prepended to LD_LIBRARY_PATH here.</summary>
    internal const string MuslLibVariable = "AGENTSMITH_MUSL_LIB";

    internal static void Scrub(ProcessStartInfo info, IReadOnlyDictionary<string, string>? stepEnv)
    {
        ArgumentNullException.ThrowIfNull(info);
        foreach (var key in AgentOnly)
            if (stepEnv?.ContainsKey(key) != true) info.Environment.Remove(key);
        DropLauncherLibraryPath(info, stepEnv);
    }

    private static void DropLauncherLibraryPath(ProcessStartInfo info, IReadOnlyDictionary<string, string>? stepEnv)
    {
        var launcherLib = info.Environment.TryGetValue(MuslLibVariable, out var lib) ? lib : null;
        info.Environment.Remove(MuslLibVariable);
        if (string.IsNullOrEmpty(launcherLib) || stepEnv?.ContainsKey("LD_LIBRARY_PATH") == true) return;
        if (!info.Environment.TryGetValue("LD_LIBRARY_PATH", out var path) || string.IsNullOrEmpty(path)) return;

        var rest = path.Split(':').Where(entry => entry != launcherLib).ToArray();
        if (rest.Length == 0) info.Environment.Remove("LD_LIBRARY_PATH");
        else info.Environment["LD_LIBRARY_PATH"] = string.Join(':', rest);
    }
}
