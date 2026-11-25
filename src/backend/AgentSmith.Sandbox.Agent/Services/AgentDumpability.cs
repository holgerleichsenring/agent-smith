using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// 2026-11-25-9992d: makes the agent non-dumpable at startup (prctl PR_SET_DUMPABLE 0), so a command of the same uid
/// cannot read /proc/&lt;agent&gt;/environ — the agent's environment holds every repository's scoped secrets. Linux
/// only; glibc's and musl's libc are both tried. A failure is a warning, never fatal: the agent still runs, and only
/// this hardening is missing.
/// </summary>
internal sealed class AgentDumpability(ILogger<AgentDumpability> logger)
{
    private const int PrSetDumpable = 4; // not a limit: the prctl option number of PR_SET_DUMPABLE

    private static readonly string[] LibcCandidates =
        ["libc", "libc.so.6", "libc.musl-x86_64.so.1", "libc.musl-aarch64.so.1"];

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate int Prctl(int option, nuint arg2, nuint arg3, nuint arg4, nuint arg5);

    public void Disable()
    {
        if (!OperatingSystem.IsLinux()) return;
        var prctl = Resolve();
        if (prctl is null)
        {
            logger.LogWarning("prctl was not found in any libc; the agent stays dumpable");
            return;
        }
        if (prctl(PrSetDumpable, 0, 0, 0, 0) != 0)
            logger.LogWarning("prctl(PR_SET_DUMPABLE, 0) failed with errno {Errno}; the agent stays dumpable",
                Marshal.GetLastPInvokeError());
    }

    private Prctl? Resolve()
    {
        foreach (var candidate in LibcCandidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var library)) continue;
            if (NativeLibrary.TryGetExport(library, "prctl", out var address))
                return Marshal.GetDelegateForFunctionPointer<Prctl>(address);
            logger.LogDebug("{Library} has no prctl export", candidate);
        }
        return null;
    }
}
