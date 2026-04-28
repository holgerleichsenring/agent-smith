using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// File-backed implementation of <see cref="ISkillsCacheMarker"/>.
/// Stores the active catalog version as a single line in <c>{cacheDir}/.pulled</c>.
/// </summary>
public sealed class SkillsCacheMarker(ILogger<SkillsCacheMarker> logger) : ISkillsCacheMarker
{
    private const string MarkerFile = ".pulled";

    public string? Read(string cacheDir)
    {
        var path = Path.Combine(cacheDir, MarkerFile);
        if (!File.Exists(path))
            return null;

        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Failed to read skill cache marker at {Path}", path);
            return null;
        }
    }

    public void Write(string cacheDir, string version)
    {
        Directory.CreateDirectory(cacheDir);
        var path = Path.Combine(cacheDir, MarkerFile);
        File.WriteAllText(path, version);
        logger.LogDebug("Wrote skill cache marker {Path} = {Version}", path, version);
    }
}
