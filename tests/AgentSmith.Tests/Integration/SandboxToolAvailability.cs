using System.Diagnostics;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0197: cached "is this command on PATH?" lookups. Tests skip when the
/// tool isn't available so CI runners without (e.g.) npm don't fail the
/// suite — but the test passes through and exercises the real binary when
/// it IS available.
/// </summary>
internal static class SandboxToolAvailability
{
    private static readonly Dictionary<string, bool> Cache = new();
    private static readonly object Gate = new();

    public static bool IsAvailable(string command)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(command, out var cached)) return cached;
            var ok = TryRun(command, "--version");
            Cache[command] = ok;
            return ok;
        }
    }

    private static bool TryRun(string command, string args)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            process.WaitForExit(5_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
