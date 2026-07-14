using System.Diagnostics;

namespace AgentSmith.Application.Services.Demo;

/// <summary>
/// p0326: host-side `git init` + initial commit for the demo workspace. Runs
/// git as a local process on purpose: the workspace is materialized BEFORE any
/// sandbox exists (SandboxGitOperations needs an ISandbox), and the demo's CLI
/// composition executes on the operator's machine where git is a hard
/// prerequisite anyway.
/// </summary>
public sealed class LocalGitProcessInitializer : IDemoGitInitializer
{
    public async Task InitializeAsync(string workspaceDir, CancellationToken cancellationToken)
    {
        await RunGitAsync(workspaceDir, ["init", "--initial-branch=main"], cancellationToken);
        await RunGitAsync(workspaceDir, ["add", "-A"], cancellationToken);
        await RunGitAsync(workspaceDir,
        [
            "-c", "user.email=demo@agent-smith.local",
            "-c", "user.name=Agent Smith Demo",
            "commit", "-m", "Demo sample project with seeded bug",
        ], cancellationToken);
    }

    private static async Task RunGitAsync(
        string workspaceDir, string[] args, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(workspaceDir, args) };
        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed (exit {process.ExitCode}): {stderr.Trim()}");
    }

    private static ProcessStartInfo BuildStartInfo(string workspaceDir, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspaceDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }
}
