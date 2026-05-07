using System.Diagnostics;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Clones a remote git source to a host tempdir via the system 'git' binary.
/// Mirrors the credential helper used by CheckoutSourceHandler's sandbox clone:
/// GIT_TOKEN drives an inline credential.helper. Token comes from the
/// platform-appropriate env var (GITHUB_TOKEN / GITLAB_TOKEN / AZURE_DEVOPS_TOKEN).
/// </summary>
public sealed class HostSourceCloner(ILogger<HostSourceCloner> logger) : IHostSourceCloner
{
    private const int CloneTimeoutSeconds = 300;
    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    public async Task<string?> TryCloneAsync(SourceConfig source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Url)) return null;
        var tempDir = CreateTempDir();
        var psi = BuildStartInfo(source, tempDir);
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

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static ProcessStartInfo BuildStartInfo(SourceConfig source, string targetDir)
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
        var token = ResolveToken(source.Type);
        if (token is not null) psi.Environment["GIT_TOKEN"] = token;
        return psi;
    }

    private static string? ResolveToken(string sourceType) => sourceType switch
    {
        var t when t.Equals("GitHub", StringComparison.OrdinalIgnoreCase)
            => Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
        var t when t.Equals("GitLab", StringComparison.OrdinalIgnoreCase)
            => Environment.GetEnvironmentVariable("GITLAB_TOKEN"),
        var t when t.Equals("AzureRepos", StringComparison.OrdinalIgnoreCase)
            => Environment.GetEnvironmentVariable("AZURE_DEVOPS_TOKEN"),
        _ => null,
    };

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
