using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class BinaryInjector(ILogger<BinaryInjector> logger)
{
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
        File.Copy(source, targetPath, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(targetPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        // p0357: the python payload rides the same injection — /shared/python next to
        // the agent binary. Absent payload (older carrier) is a silent no-op.
        if (!string.IsNullOrEmpty(directory))
        {
            new PythonPayloadCopier(logger).CopyIfPresent(
                PythonPayloadCopier.CarrierPayloadPath,
                Path.Combine(directory, PythonPayloadCopier.PayloadDirName));
        }
        logger.LogInformation("Inject complete");
        return 0;
    }
}
