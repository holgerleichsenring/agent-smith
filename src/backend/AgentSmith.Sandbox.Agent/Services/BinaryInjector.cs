using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// Copies the agent into the shared volume the toolchain container runs it from. 2026-10-16-8648:
/// a carrier with a launcher puts the launcher at the target path and both agent builds beside it
/// (agent-gnu, musl/), so one command line starts on glibc and musl images alike. A carrier
/// without one copies this binary to the target, as before.
/// </summary>
internal sealed class BinaryInjector(
    ILogger<BinaryInjector> logger,
    string launcherPath = BinaryInjector.CarrierLauncherPath,
    string muslRoot = BinaryInjector.CarrierMuslPath)
{
    public const string CarrierLauncherPath = "/launcher";
    public const string CarrierMuslPath = "/musl";
    public const string GnuAgentName = "agent-gnu";
    public const string MuslDirName = "musl";

    public int Inject(string targetPath)
    {
        var source = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve own binary path (Environment.ProcessPath is null)");
        logger.LogInformation("Injecting agent binary {Source} -> {Target}", source, targetPath);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (File.Exists(launcherPath) && !string.IsNullOrEmpty(directory))
        {
            CopyExecutable(source, Path.Combine(directory, GnuAgentName));
            CopyExecutable(launcherPath, targetPath);
            new PayloadTreeCopier(logger).CopyIfPresent(muslRoot, Path.Combine(directory, MuslDirName));
        }
        else
        {
            CopyExecutable(source, targetPath);
        }
        // p0357: the python payload rides the same injection — /shared/python next to
        // the agent binary. Absent payload (older carrier) is a silent no-op.
        if (!string.IsNullOrEmpty(directory))
        {
            new PayloadTreeCopier(logger).CopyIfPresent(
                PayloadTreeCopier.CarrierPythonPath,
                Path.Combine(directory, PayloadTreeCopier.PythonDirName));
        }
        logger.LogInformation("Inject complete");
        return 0;
    }

    private static void CopyExecutable(string source, string target)
    {
        File.Copy(source, target, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}
