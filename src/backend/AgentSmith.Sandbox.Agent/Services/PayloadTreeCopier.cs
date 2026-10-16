using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// p0357: copies a payload tree of the carrier image next to the injected agent binary — the
/// relocatable CPython (/python, from python-build-standalone) so the toolchain container has
/// python3 regardless of its image, and since 2026-10-16-8648 the musl agent (/musl). Symlinks are re-created as
/// links (bin/python3 -> python3.12) and unix modes are preserved — a byte-copied
/// symlink or a non-executable interpreter would break the payload silently.
/// Absent payload is a no-op: the agent inject must never fail because a carrier
/// build predates the payload.
/// </summary>
internal sealed class PayloadTreeCopier(ILogger logger)
{
    /// <summary>Where the carrier image stages the python payload (Dockerfile stage python-fetch).</summary>
    public const string CarrierPythonPath = "/python";

    /// <summary>Directory name of the python payload next to the injected agent binary.</summary>
    public const string PythonDirName = "python";

    public void CopyIfPresent(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            logger.LogDebug("No payload at {Source} — skipping", sourceRoot);
            return;
        }
        logger.LogInformation("Injecting payload {Source} -> {Target}", sourceRoot, targetRoot);
        CopyTree(sourceRoot, targetRoot);
        logger.LogInformation("Payload inject complete");
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
