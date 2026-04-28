using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Downloads, verifies (SHA256), and atomically extracts the skill catalog
/// tarball. Default release URL is the <c>holgerleichsenring/agent-smith-skills</c>
/// GitHub release; overridable via the <c>AGENTSMITH_SKILLS_REPOSITORY_URL</c>
/// environment variable for air-gap mirrors.
/// </summary>
public sealed class SkillsRepositoryClient(
    HttpClient httpClient,
    ILogger<SkillsRepositoryClient> logger) : ISkillsRepositoryClient
{
    private const string DefaultRepoUrl = "https://github.com/holgerleichsenring/agent-smith-skills";
    private const string EnvOverride = "AGENTSMITH_SKILLS_REPOSITORY_URL";

    public Uri ResolveReleaseUrl(string version)
    {
        var baseUrl = Environment.GetEnvironmentVariable(EnvOverride);
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultRepoUrl;

        baseUrl = baseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}/releases/download/{version}/agentsmith-skills-{version}.tar.gz");
    }

    public async Task PullAsync(
        Uri tarballUrl,
        string outputDir,
        string? expectedSha256,
        CancellationToken cancellationToken)
    {
        var tarballPath = Path.Combine(Path.GetTempPath(),
            $"agentsmith-skills-{Guid.NewGuid():N}.tar.gz");
        var stagingDir = outputDir + ".pulling-" + Guid.NewGuid().ToString("N")[..8];

        try
        {
            await DownloadAsync(tarballUrl, tarballPath, cancellationToken);
            VerifySha256(tarballPath, expectedSha256);
            ExtractAtomically(tarballPath, stagingDir, outputDir);
            logger.LogInformation("Catalog extracted to {OutputDir} from {Url}", outputDir, tarballUrl);
        }
        finally
        {
            TryDelete(tarballPath);
            TryDeleteDirectory(stagingDir);
        }
    }

    private async Task DownloadAsync(Uri url, string destination, CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading skill catalog from {Url}", url);
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var fs = File.Create(destination);
        await response.Content.CopyToAsync(fs, cancellationToken);
    }

    private static void VerifySha256(string tarballPath, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return;

        using var fs = File.OpenRead(tarballPath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(fs);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        var normalised = expected.Trim().Split(' ', '\t')[0].ToLowerInvariant();
        if (actual != normalised)
        {
            throw new InvalidOperationException(
                $"SHA256 mismatch for {tarballPath}: expected {normalised}, got {actual}");
        }
    }

    private static void ExtractAtomically(string tarballPath, string stagingDir, string finalDir)
    {
        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, recursive: true);
        Directory.CreateDirectory(stagingDir);

        using (var fs = File.OpenRead(tarballPath))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        {
            TarFile.ExtractToDirectory(gz, stagingDir, overwriteFiles: true);
        }

        // Atomic-ish swap: replace finalDir contents from staging.
        if (Directory.Exists(finalDir))
            Directory.Delete(finalDir, recursive: true);

        var parent = Path.GetDirectoryName(finalDir);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        Directory.Move(stagingDir, finalDir);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* swallow — best-effort cleanup */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { /* swallow — best-effort cleanup */ }
    }
}
