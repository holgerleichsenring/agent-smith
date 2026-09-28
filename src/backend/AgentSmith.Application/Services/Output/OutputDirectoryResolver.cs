using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Output;

public sealed class OutputDirectoryResolver(
    ILogger<OutputDirectoryResolver> logger) : IOutputDirectoryResolver
{
    private const string ContainerOutputDir = "/output";
    private const string LocalOutputDir = "./agentsmith-output";

    public string Resolve(string? requested)
    {
        foreach (var candidate in new[] { requested, ContainerOutputDir, LocalOutputDir })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (IsWritable(candidate)) return candidate;
        }

        var temp = Path.GetTempPath();
        logger.LogWarning(
            "All preferred output dirs unwritable; output falls back to temp dir '{Temp}'", temp);
        return temp;
    }

    private bool IsWritable(string candidate)
    {
        try
        {
            Directory.CreateDirectory(candidate);
            var testFile = Path.Combine(candidate, ".write-test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "Output dir '{Candidate}' not writable, trying next fallback", candidate);
            return false;
        }
    }
}
