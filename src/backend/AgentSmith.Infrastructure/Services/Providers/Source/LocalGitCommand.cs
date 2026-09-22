using System.Diagnostics;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-22-b6ad: one git invocation against a repository on disk, with an optional stdin and
/// an optional SCRATCH INDEX. The scratch index is the whole point: it is what lets a commit be
/// made on a branch the operator is not on without their staged changes joining it and without
/// its files appearing in their working tree.
/// </summary>
internal sealed record LocalGitResult(int Exit, string Out, string Error)
{
    /// <summary>git's own words for what went wrong, or a named failure when it said nothing.</summary>
    internal string Reason(string step) =>
        Error.Trim() is { Length: > 0 } text ? $"git {step}: {text}" : $"git {step} failed";
}

/// <inheritdoc cref="LocalGitResult"/>
internal static class LocalGitCommand
{
    private const int TimeoutSeconds = 60;
    private const string Identity = "agent-smith";
    private const string IdentityEmail = "agent-smith@localhost";

    internal static async Task<LocalGitResult> RunAsync(
        string repoPath, string? indexFile, string? stdin, CancellationToken ct, params string[] args)
    {
        using var process = Process.Start(StartInfo(repoPath, indexFile, args))
            ?? throw new InvalidOperationException("Failed to start git process");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin.AsMemory(), timeout.Token);
            process.StandardInput.Close();
        }
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        return new LocalGitResult(process.ExitCode, await output, await error);
    }

    private static ProcessStartInfo StartInfo(string repoPath, string? indexFile, string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoPath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        if (indexFile is not null) psi.Environment["GIT_INDEX_FILE"] = indexFile;
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // commit-tree refuses without an identity, and a local repository need not have one.
        psi.Environment["GIT_AUTHOR_NAME"] = Identity;
        psi.Environment["GIT_AUTHOR_EMAIL"] = IdentityEmail;
        psi.Environment["GIT_COMMITTER_NAME"] = Identity;
        psi.Environment["GIT_COMMITTER_EMAIL"] = IdentityEmail;
        return psi;
    }
}
