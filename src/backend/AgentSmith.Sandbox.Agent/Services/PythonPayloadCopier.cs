using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// p0357: copies the carrier image's relocatable CPython payload (/python, from
/// python-build-standalone) next to the injected agent binary so the toolchain
/// container has python3 regardless of its image. Symlinks are re-created as
/// links (bin/python3 -> python3.12) and unix modes are preserved — a byte-copied
/// symlink or a non-executable interpreter would break the payload silently.
/// Absent payload is a no-op: the agent inject must never fail because a carrier
/// build predates the payload.
/// </summary>
internal sealed class PythonPayloadCopier(ILogger logger)
{
    /// <summary>Where the carrier image stages the payload (Dockerfile stage python-fetch).</summary>
    public const string CarrierPayloadPath = "/python";

    /// <summary>Directory name of the payload next to the injected agent binary.</summary>
    public const string PayloadDirName = "python";

    public void CopyIfPresent(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            logger.LogDebug("No python payload at {Source} — skipping", sourceRoot);
            return;
        }
        logger.LogInformation("Injecting python payload {Source} -> {Target}", sourceRoot, targetRoot);
        CopyTree(sourceRoot, targetRoot);
        logger.LogInformation("Python payload inject complete");
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source))
            CopyTree(dir, Path.Combine(target, Path.GetFileName(dir)));
        foreach (var file in Directory.GetFiles(source))
            CopyEntry(file, Path.Combine(target, Path.GetFileName(file)));
    }

    private static void CopyEntry(string source, string target)
    {
        var info = new FileInfo(source);
        if (info.LinkTarget is { } linkTarget)
        {
            if (File.Exists(target)) File.Delete(target);
            File.CreateSymbolicLink(target, linkTarget);
            return;
        }
        File.Copy(source, target, overwrite: true);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(target, File.GetUnixFileMode(source));
    }
}
