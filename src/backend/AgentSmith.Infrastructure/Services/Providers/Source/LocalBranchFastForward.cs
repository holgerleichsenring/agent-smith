using System.Diagnostics;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: moves a local repository's default branch onto a work branch, which is what
/// "completing the pull request" means where there is no pull request server. The move
/// is <c>git fetch . src:dst</c>, because that form refuses a non-fast-forward and works
/// while the default branch is not the checked-out one — the init run leaves the working
/// tree on the init branch. A refusal comes back as a reason, never as a throw.
/// </summary>
public sealed class LocalBranchFastForward
{
    private const int TimeoutSeconds = 30;

    public async Task<PullRequestCompletion> RunAsync(
        string repoPath, string sourceBranch, string targetBranch, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(repoPath))
            return PullRequestCompletion.Refused($"Local path not found: {repoPath}");
        if (string.Equals(sourceBranch, targetBranch, StringComparison.Ordinal))
            return PullRequestCompletion.Refused(
                $"'{sourceBranch}' is already the default branch — there is nothing to fast-forward.");
        try
        {
            var (exit, error) = await RunGitAsync(repoPath, sourceBranch, targetBranch, cancellationToken);
            return exit == 0
                ? PullRequestCompletion.Merged()
                : PullRequestCompletion.Refused(Describe(error, targetBranch, sourceBranch));
        }
        catch (Exception ex)
        {
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    private static async Task<(int Exit, string Error)> RunGitAsync(
        string repoPath, string sourceBranch, string targetBranch, CancellationToken cancellationToken)
    {
        using var process = Process.Start(BuildStartInfo(repoPath, sourceBranch, targetBranch))
            ?? throw new InvalidOperationException("Failed to start git process");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        await process.WaitForExitAsync(timeout.Token);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        return (process.ExitCode, error);
    }

    private static ProcessStartInfo BuildStartInfo(
        string repoPath, string sourceBranch, string targetBranch)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("fetch");
        psi.ArgumentList.Add(".");
        psi.ArgumentList.Add($"{sourceBranch}:{targetBranch}");
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return psi;
    }

    // git's own wording for a rejected fast-forward is "non-fast-forward"; say what
    // that means for this repository instead of handing the operator raw plumbing.
    private static string Describe(string error, string targetBranch, string sourceBranch) =>
        error.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase)
            ? $"'{targetBranch}' has commits '{sourceBranch}' does not — it cannot be fast-forwarded."
            : error.Trim() is { Length: > 0 } text ? text : "git refused the fast-forward.";
}
