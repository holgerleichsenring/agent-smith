using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Wraps an ISandbox with a typed file-IO surface. Each method builds the
/// appropriate Step record and translates the result into typed values.
/// ExistsAsync probes via ReadFile and treats a non-zero exit as "missing".
/// </summary>
internal sealed class SandboxFileReader(ISandbox sandbox) : ISandboxFileReader
{
    private const int FileTimeoutSeconds = 30;

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(MakeStep(StepKind.ReadFile, path), null, cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<string?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(MakeStep(StepKind.ReadFile, path), null, cancellationToken);
        return result.ExitCode == 0 ? result.OutputContent ?? string.Empty : null;
    }

    public async Task<string> ReadRequiredAsync(string path, CancellationToken cancellationToken)
    {
        var content = await TryReadAsync(path, cancellationToken);
        if (content is null)
            throw new FileNotFoundException($"File not found in sandbox: {path}", path);
        return content;
    }

    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            MakeStep(StepKind.WriteFile, path, content: content), null, cancellationToken);
        if (result.ExitCode != 0)
            throw new IOException(
                $"Failed to write file '{path}' in sandbox: {result.ErrorMessage ?? "unknown error"}");
    }

    public async Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken cancellationToken)
    {
        var result = await sandbox.RunStepAsync(
            MakeStep(StepKind.ListFiles, path, maxDepth: maxDepth), null, cancellationToken);
        if (result.ExitCode != 0 || result.OutputContent is null)
            return Array.Empty<string>();
        return JsonSerializer.Deserialize<string[]>(result.OutputContent) ?? Array.Empty<string>();
    }

    private static Step MakeStep(StepKind kind, string path, string? content = null, int? maxDepth = null) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), kind,
            TimeoutSeconds: FileTimeoutSeconds, Path: path, Content: content, MaxDepth: maxDepth);
}
