using System.Diagnostics;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 docker-tier guard. Probes <c>docker ps</c>; on exit ≠ 0 we
/// LOG (not silently skip) that the docker-required registry-auth
/// coverage tier is NOT exercised on this machine, then signal back to
/// the test so it can route to the matching Skip path. Skipping silently
/// is forbidden — operators need to see when their CI lane isn't running
/// the falsifiability-anchor tests.
/// </summary>
internal static class DockerAvailability
{
    public const string OptInEnv = "AGENTSMITH_HARNESS_DOCKER";

    public static bool IsAvailable(out string detail)
    {
        var optIn = Environment.GetEnvironmentVariable(OptInEnv);
        if (optIn != "1")
        {
            detail = $"set {OptInEnv}=1 to enable; docker daemon must be reachable";
            return false;
        }
        return DockerDaemonReachable(out detail);
    }

    private static bool DockerDaemonReachable(out string detail)
    {
        try
        {
            var psi = new ProcessStartInfo("docker", "ps")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            if (p is null) { detail = "Process.Start returned null"; return false; }
            p.WaitForExit(5000);
            if (p.ExitCode == 0) { detail = "docker ps exit=0"; return true; }
            detail = $"docker ps exit={p.ExitCode}";
            return false;
        }
        catch (Exception ex)
        {
            detail = "docker probe threw: " + ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    public const string CoverageNotExercised =
        "DOCKER TIER NOT EXERCISED — set AGENTSMITH_HARNESS_DOCKER=1 to enable. " +
        "The docker-tier tier reproduces the p0198 NU1301 falsifiability anchor " +
        "(clean container, missing registries block → dotnet restore fails) and the " +
        "full p97b689d PersistWorkBranch wiring against a real bare git remote. " +
        "This skip is a known coverage gap on developer machines without docker " +
        "(or without the opt-in env var), NOT a passing test.";
}
