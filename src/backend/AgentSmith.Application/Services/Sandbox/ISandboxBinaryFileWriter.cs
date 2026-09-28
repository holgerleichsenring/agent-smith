using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Places a binary file into a sandbox directory byte for byte. The sandbox file surface
/// writes strings, so the bytes travel as base64 and are decoded in the sandbox.
/// </summary>
public interface ISandboxBinaryFileWriter
{
    /// <returns>null on success, otherwise why the file did not arrive.</returns>
    Task<string?> WriteAsync(
        ISandbox sandbox, string directory, string fileName, byte[] content,
        CancellationToken cancellationToken);
}
