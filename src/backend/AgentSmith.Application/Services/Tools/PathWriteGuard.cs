using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Validates that a path is writable for the active skill phase. Read-scope check
/// runs first via <see cref="IPathReadGuard"/>; then phase-gating: only Implementation
/// and Bootstrap may write. Bootstrap is further restricted to the two bootstrap
/// files under the round's per-context MetaDir
/// (<c>.agentsmith/contexts/&lt;ContextName&gt;/{context.yaml,coding-principles.md}</c>).
/// Empty contextName falls back to the legacy flat layout
/// (<c>.agentsmith/{context.yaml,coding-principles.md}</c>) for pre-p0161d test
/// fixtures.
/// </summary>
public sealed class PathWriteGuard : IPathWriteGuard
{
    private readonly IPathReadGuard _readGuard;
    private readonly SkillExecutionPhase _phase;
    private readonly string[] _bootstrapFiles;

    public PathWriteGuard(IPathReadGuard readGuard, SkillExecutionPhase phase)
        : this(readGuard, phase, contextName: null) { }

    public PathWriteGuard(IPathReadGuard readGuard, SkillExecutionPhase phase, string? contextName)
    {
        _readGuard = readGuard;
        _phase = phase;
        _bootstrapFiles = BuildBootstrapFileList(contextName);
    }

    private static string[] BuildBootstrapFileList(string? contextName)
    {
        if (string.IsNullOrEmpty(contextName))
            return [ProjectMetaPaths.ContextYaml, ProjectMetaPaths.CodingPrinciples];
        var metaDir = $"{ProjectMetaPaths.Contexts}/{contextName}";
        return
        [
            $"{metaDir}/{ProjectMetaPaths.ContextYamlFile}",
            $"{metaDir}/{ProjectMetaPaths.CodingPrinciplesFile}",
        ];
    }

    public Result AssertWritable(string path)
    {
        var readResult = _readGuard.AssertReadable(path);
        if (!readResult.IsSuccess)
            return readResult;

        if (_phase != SkillExecutionPhase.Implementation && _phase != SkillExecutionPhase.Bootstrap)
            return Fail(GuardErrorKind.WriteForbiddenInPhase, path,
                $"writes are forbidden in phase '{_phase}'; only Implementation and Bootstrap may write");

        if (_phase == SkillExecutionPhase.Bootstrap && !IsBootstrapFile(path))
            return Fail(GuardErrorKind.NotInBootstrapFiles, path,
                $"path '{path}' is not in the bootstrap-allowed file list [{string.Join(", ", _bootstrapFiles)}]");

        return Result.Ok();
    }

    private bool IsBootstrapFile(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return _bootstrapFiles.Any(bf =>
            normalized.EndsWith(bf, StringComparison.Ordinal)
            || normalized.Equals(bf, StringComparison.Ordinal));
    }

    private static Result Fail(GuardErrorKind kind, string path, string message)
        => Result.Fail(new GuardError { Kind = kind, Path = path, Message = message });
}
