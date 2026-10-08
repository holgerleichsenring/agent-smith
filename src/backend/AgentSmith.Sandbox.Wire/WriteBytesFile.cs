namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// 2026-10-08-e8b9j: what a <see cref="StepKind.WriteBytes"/> step does to the file system, in
/// C#, the same on the sandbox agent and on the in-process backend — which runs in the server,
/// whose image has no python3 to decode with. Paths arrive resolved; the result is null on
/// success or the reason the bytes did not land.
/// </summary>
public sealed class WriteBytesFile
{
    public async Task<string?> WriteAsync(
        string path, string base64, bool append, string? renameTo, CancellationToken ct)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { return "content is not base64"; }
        if (bytes.LongLength > SizeLimits.WriteBytesChunkMaxBytes)
            return $"a chunk exceeds the {SizeLimits.WriteBytesChunkMaxBytes} bytes one step may carry";
        var held = append && File.Exists(path) ? new FileInfo(path).Length : 0;
        if (held + bytes.LongLength > SizeLimits.WriteBytesMaxBytes)
            return $"the file would exceed the {SizeLimits.WriteBytesMaxBytes}-byte limit";
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
        await using (var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write))
            await stream.WriteAsync(bytes, ct);
        if (renameTo is null) return null;
        if (Path.GetDirectoryName(renameTo) is { Length: > 0 } target) Directory.CreateDirectory(target);
        File.Move(path, renameTo, overwrite: true);
        return null;
    }
}
