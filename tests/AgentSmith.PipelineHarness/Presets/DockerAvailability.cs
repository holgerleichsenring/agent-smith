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
    public static bool IsAvailable(out string detail)
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
        "DOCKER NOT AVAILABLE — REGISTRY-AUTH COVERAGE NOT EXERCISED ON THIS MACHINE. " +
        "The p0198 NU1301 falsifiability anchor (clean container, missing registries " +
        "block → dotnet restore fails) cannot be reproduced without docker. The full " +
        "registry-auth tier was reviewed by the operator at p0198 land; this skip is a " +
        "known coverage gap on developer machines without docker, NOT a passing test.";
}
