using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-08-e8b9j: a file sent as WriteBytes steps of at most
/// <see cref="SizeLimits.WriteBytesChunkMaxBytes"/> each into a temporary name beside it, the
/// last chunk moving it into place — a reader never sees half a file, and no single step is a
/// 33 MB message on the bus. It replaced a base64 file decoded by `base64 -d` in a shell.
/// </summary>
public sealed class SandboxBinaryFileWriter : ISandboxBinaryFileWriter
{
    public async Task<string?> WriteAsync(
        ISandbox sandbox, string directory, string fileName, byte[] content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(content);
        if (content.LongLength > SizeLimits.WriteBytesMaxBytes)
            return $"'{fileName}' is {content.LongLength} bytes, over the {SizeLimits.WriteBytesMaxBytes}-byte limit";
        var target = directory.Length == 0 ? fileName : $"{directory.TrimEnd('/')}/{fileName}";
        var temp = $"{target}.tmp.{Guid.NewGuid():N}";
        var offset = 0;
        do
        {
            var size = (int)Math.Min(SizeLimits.WriteBytesChunkMaxBytes, content.Length - offset);
            var last = offset + size >= content.Length;
            var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteBytes,
                Path: temp, Content: Convert.ToBase64String(content, offset, size),
                Append: offset > 0, RenameTo: last ? target : null);
            var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
            if (result.ExitCode != 0)
                return $"writing '{fileName}' into the sandbox failed: {result.ErrorMessage ?? "exit " + result.ExitCode}";
            offset += size;
        }
        while (offset < content.Length);
        return null;
    }
}
