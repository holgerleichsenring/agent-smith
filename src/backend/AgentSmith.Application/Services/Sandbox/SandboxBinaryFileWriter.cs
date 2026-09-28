using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

public sealed class SandboxBinaryFileWriter(
    ISandboxFileReaderFactory readerFactory) : ISandboxBinaryFileWriter
{
    private const int DecodeTimeoutSeconds = 120;

    public async Task<string?> WriteAsync(
        ISandbox sandbox, string directory, string fileName, byte[] content,
        CancellationToken cancellationToken)
    {
        var encoded = $"{fileName}.b64";
        await readerFactory.Create(sandbox).WriteAsync(
            $"{directory}/{encoded}", Convert.ToBase64String(content), cancellationToken);

        // Relative names under the step's working directory: every sandbox backend maps
        // the directory, none rewrites paths inside a shell command.
        var decode = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", $"base64 -d < {Quote(encoded)} > {Quote(fileName)} && rm -f {Quote(encoded)}"],
            WorkingDirectory: directory,
            TimeoutSeconds: DecodeTimeoutSeconds);
        var result = await sandbox.RunStepAsync(decode, progress: null, cancellationToken);
        return result.ExitCode == 0
            ? null
            : $"decoding '{fileName}' in the sandbox failed (exit {result.ExitCode}): {result.ErrorMessage}";
    }

    private static string Quote(string value) => $"'{value.Replace("'", "'\\''")}'";
}
