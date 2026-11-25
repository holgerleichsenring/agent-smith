using System.Diagnostics;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// 2026-11-25-9992d: what a command's process sees of the repository-scoped secrets every toolchain container carries
/// (<see cref="ScopedSecretNames"/>). Every scoped variable is dropped; when the step names a repository, that
/// repository's come back under their plain names — unless the step's own env sets the name, which wins.
/// <para>
/// This keeps another repository's credential out of a command's environment. It does not make it unreachable: a
/// command running at the same time for another repository is a process of the same uid, and a file in the container
/// is readable by every command of it. The agent itself is made non-dumpable (<see cref="AgentDumpability"/>) so a
/// command cannot read the full set off the agent's own /proc environ.
/// </para>
/// </summary>
internal static class ScopedSecretEnvironment
{
    internal static void Apply(
        ProcessStartInfo info, IReadOnlyDictionary<string, string>? stepEnv, string? secretScope)
    {
        var scoped = info.Environment.Where(e => ScopedSecretNames.IsScoped(e.Key)).ToList();
        foreach (var (variable, _) in scoped) info.Environment.Remove(variable);
        if (string.IsNullOrEmpty(secretScope)) return;
        foreach (var (variable, value) in scoped)
        {
            if (!ScopedSecretNames.TryParse(variable, out var repository, out var name)) continue;
            if (!string.Equals(repository, secretScope, StringComparison.Ordinal)) continue;
            if (stepEnv?.ContainsKey(name) == true) continue;
            info.Environment[name] = value;
        }
    }
}
