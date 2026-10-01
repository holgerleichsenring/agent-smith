using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283df: keeps the uploaded websites a run carries out of <c>git add -A</c> by naming
/// their directory in the clone's own <c>.git/info/exclude</c> — local to this checkout, never
/// committed, so the repository's .gitignore is not touched. A force-stage ignores excludes; that
/// half is <see cref="ReferenceDirectory.ExcludePathspec"/>.
/// </summary>
public sealed class ReferenceGitExclusion(ISandboxFileReaderFactory files)
{
    internal const string ExcludeFile = ".git/info/exclude";

    public async Task EnsureAsync(ISandbox sandbox, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var io = files.Create(sandbox);
        var current = await io.TryReadAsync(ExcludeFile, ct) ?? string.Empty;
        if (current.Split('\n').Any(line => line.Trim() == ReferenceDirectory.ExcludeLine)) return;
        var separator = current.Length == 0 || current.EndsWith('\n') ? string.Empty : "\n";
        await io.WriteAsync(ExcludeFile, current + separator + ReferenceDirectory.ExcludeLine + "\n", ct);
    }
}
