using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Places a binary file into a sandbox directory byte for byte. 2026-10-08-e8b9j: through the
/// WriteBytes step, decoded by the receiver in C#, so no sandbox needs a shell or python to take
/// one — the in-process backend has neither python3 nor, in the server image, any reason to.
/// </summary>
public interface ISandboxBinaryFileWriter
{
    /// <param name="fileName">The file's path under <paramref name="directory"/>; may hold folders.</param>
    /// <returns>null on success, otherwise why the file did not arrive.</returns>
    Task<string?> WriteAsync(
        ISandbox sandbox, string directory, string fileName, byte[] content,
        CancellationToken cancellationToken);
}
