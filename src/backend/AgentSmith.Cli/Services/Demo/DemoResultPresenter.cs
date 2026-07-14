using System.Diagnostics;
using AgentSmith.Domain.Models;

namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: renders the demo's outcome — the run summary, the recorded commit's
/// diff (`git diff HEAD~1` in the workspace), and the next-steps pointer.
/// Fail-soft: a diff-printing hiccup must never turn a green run red.
/// </summary>
internal sealed class DemoResultPresenter
{
    public async Task PresentAsync(
        CommandResult result, string workspace, TextWriter output, CancellationToken cancellationToken)
    {
        output.WriteLine();
        output.WriteLine(result.IsSuccess ? $"Demo run succeeded: {result.Message}" : $"Demo run failed: {result.Message}");
        await PrintDiffAsync(workspace, output, cancellationToken);
        output.WriteLine();
        output.WriteLine($"The workspace stays at {workspace} for inspection.");
        output.WriteLine("Next: connect your real tracker and repos — docs/get-it-running/first-run.md, step 2.");
    }

    private async Task PrintDiffAsync(string workspace, TextWriter output, CancellationToken ct)
    {
        try
        {
            var count = await GitAsync(workspace, ["rev-list", "--count", "HEAD"], ct);
            if (!int.TryParse(count.Trim(), out var commits) || commits < 2)
            {
                output.WriteLine("No new commit was recorded on top of the seeded baseline (see the run summary above).");
                return;
            }
            output.WriteLine(await GitAsync(workspace, ["log", "-1", "--format=Recorded commit: %h %s"], ct));
            output.WriteLine(await GitAsync(workspace, ["diff", "HEAD~1", "--stat"], ct));
            output.WriteLine(await GitAsync(workspace, ["diff", "HEAD~1"], ct));
        }
        catch (Exception ex)
        {
            output.WriteLine($"Could not render the diff ({ex.Message}) — inspect it with: git -C {workspace} diff HEAD~1");
        }
    }

    private static async Task<string> GitAsync(string workspace, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0
            ? stdout.TrimEnd()
            : throw new InvalidOperationException($"git {string.Join(' ', args)} exited {process.ExitCode}");
    }
}
