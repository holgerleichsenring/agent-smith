using System.Diagnostics;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Clones a remote git source to a host tempdir via the system 'git' binary.
/// Mirrors the credential helper used by CheckoutSourceHandler's sandbox clone:
/// GIT_TOKEN drives an inline credential.helper. 2026-10-02-5f89g: the token is the repo's
/// own auth secret, through the same <see cref="IGitTokenResolver"/> the sandbox clone uses.
/// </summary>
public sealed class HostSourceCloner(IGitTokenResolver credentials, ILogger<HostSourceCloner> logger)
    : IHostSourceCloner
{
    private const int CloneTimeoutSeconds = 300;
    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    public async Task<string?> TryCloneAsync(RepoConnection source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Url)) return null;
        if (Credential(source) is not { } credential) return null;
        var tempDir = CreateTempDir();
        var psi = BuildStartInfo(source, credential, tempDir);
        try
        {
            var (exit, stderr) = await RunAsync(psi, cancellationToken);
            if (exit == 0) return tempDir;
            logger.LogWarning("git clone failed (exit={Exit}): {Err}", exit, stderr.Trim());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "git clone process failed: {Message}", ex.Message);
        }
        TryDelete(tempDir);
        return null;
    }

    private GitCredential? Credential(RepoConnection source)
    {
        try
        {
            return credentials.For(source);
        }
        catch (MissingCredentialException ex)
        {
            logger.LogWarning("git clone of {Repo} not attempted: {Reason}", source.Name, ex.Message);
            return null;
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static ProcessStartInfo BuildStartInfo(
        RepoConnection source, GitCredential credential, string targetDir)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(CredHelper);
        psi.ArgumentList.Add("clone");
        psi.ArgumentList.Add("--depth=1");
        psi.ArgumentList.Add("--no-tags");
        psi.ArgumentList.Add(source.Url!);
        psi.ArgumentList.Add(targetDir);
        if (credential.HasToken) psi.Environment["GIT_TOKEN"] = credential.Token;
        // p0419: the clone carries its own credential helper (CredHelper above), so the
        // SYSTEM gitconfig has nothing to add — and on macOS it declares the keychain
        // helper, which puts a modal dialog in front of an operator who started a
        // headless run. A background clone must never wait on a human.
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return psi;
    }

    private static async Task<(int exit, string stderr)> RunAsync(
        ProcessStartInfo psi, CancellationToken cancellationToken)
    {
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(CloneTimeoutSeconds));
        await process.WaitForExitAsync(timeoutCts.Token);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        return (process.ExitCode, stderr);
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }
}
