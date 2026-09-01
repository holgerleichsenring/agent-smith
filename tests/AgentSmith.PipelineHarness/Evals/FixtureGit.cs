using System.Diagnostics;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: git, for the fixtures that need a real repository.
/// <para>
/// A failed setup command throws, never warns: a fixture built wrong would score the thing
/// under test against a tree nobody designed. Extracted from
/// <see cref="AccountFixtureRepositories"/> when the security corpus needed the same
/// commands — two copies of a subprocess runner are how two fixtures start building
/// differently shaped repositories.
/// </para>
/// </summary>
internal static class FixtureGit
{
    internal static async Task RunAsync(string workDir, CancellationToken ct, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("git could not be started");
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed in {workDir}: {stderr}");
    }

    /// <summary>An identity and an initial branch, so a fresh fixture repository commits
    /// the same way on a machine with no global git config.</summary>
    internal static async Task InitAsync(string path, string branch, CancellationToken ct)
    {
        await RunAsync(path, ct, "init", "--quiet", "--initial-branch", branch);
        await RunAsync(path, ct, "config", "user.email", "fixture@example.com");
        await RunAsync(path, ct, "config", "user.name", "Fixture");
    }
}
