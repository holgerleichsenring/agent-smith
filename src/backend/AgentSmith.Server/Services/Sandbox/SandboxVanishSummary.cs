namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0396: maps the container EXIT EVIDENCE the liveness watcher inspected
/// (exit code + OOMKilled) to the operator-facing vanish sentence. The old
/// blanket "most often an out-of-memory kill" guess misdiagnosed an agent
/// crash (exit 3 — e.g. a Redis client timeout during the idle wait) as a
/// memory problem and sent the operator tuning memory limits; the OOM
/// guidance now appears only when Docker actually reports OOMKilled.
/// </summary>
internal static class SandboxVanishSummary
{
    // AgentSmith.Sandbox.Agent Program.ExitUnhandledError — the agent's
    // catch-all exit code. Not referenced directly: the agent is a separately
    // published binary, not a server dependency.
    private const long AgentUnhandledErrorExitCode = 3;

    public static string Describe(long exitCode, bool oomKilled) => oomKilled
        ? $"The sandbox container was OOM-killed mid-run (exit code {exitCode}, oomKilled=true). "
          + "Check the sandbox container's memory limit and whether the build needs a `restore` "
          + "step first. The 'A task was cancelled' on the LLM call is a side effect, not the cause."
        : exitCode switch
        {
            AgentUnhandledErrorExitCode =>
                "The sandbox container exited mid-run (it vanished): the sandbox agent crashed "
                + "(unhandled error, exit code 3) — see the sandbox container log for the failing "
                + "operation. This was not an OOM kill (oomKilled=false).",
            0 =>
                "The sandbox container exited mid-run (it vanished) with a clean exit (exit code 0) "
                + "— the agent process ended while the run was still active; see the sandbox "
                + "container log.",
            _ =>
                $"The sandbox container exited mid-run (it vanished) with exit code {exitCode} "
                + "(oomKilled=false) — see the sandbox container log.",
        };
}
