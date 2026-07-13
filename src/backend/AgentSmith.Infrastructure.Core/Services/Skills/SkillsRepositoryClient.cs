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
    ICatalogTarballExtractor extractor,
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

        try
        {
            await DownloadAsync(tarballUrl, tarballPath, cancellationToken);
            VerifySha256(tarballPath, expectedSha256);
            // p0325: extraction shares the atomic staging+swap with the embedded path.
            using (var tarball = File.OpenRead(tarballPath))
            {
                extractor.Extract(tarball, outputDir);
            }
            logger.LogInformation("Catalog extracted to {OutputDir} from {Url}", outputDir, tarballUrl);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Cannot write to skill catalog cache directory '{outputDir}'. " +
                "Set 'skills.cache_dir' in agentsmith.yml to a writable path " +
                "(default per-user is $HOME/.cache/agentsmith/skills); the " +
                "configured directory needs write permission for the running user.",
                ex);
        }
        finally
        {
            TryDelete(tarballPath);
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

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* swallow — best-effort cleanup */ }
    }
}
