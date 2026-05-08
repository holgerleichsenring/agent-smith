using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Validates that a path is writable for the active skill phase. Read-scope check
/// runs first via <see cref="IPathReadGuard"/>; then phase-gating: only Implementation
/// and Bootstrap may write. Bootstrap is further restricted to the two bootstrap
/// files (.agentsmith/context.yaml, .agentsmith/coding-principles.md). The
/// Implementation-phase plan-file restriction is deferred to p0128.
/// </summary>
public sealed class PathWriteGuard : IPathWriteGuard
{
    private static readonly string[] BootstrapFiles =
    {
        ".agentsmith/context.yaml",
        ".agentsmith/coding-principles.md"
    };

    private readonly IPathReadGuard _readGuard;
    private readonly SkillExecutionPhase _phase;

    public PathWriteGuard(IPathReadGuard readGuard, SkillExecutionPhase phase)
    {
        _readGuard = readGuard;
        _phase = phase;
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
                $"path '{path}' is not in the bootstrap-allowed file list");

        return Result.Ok();
    }

    private static bool IsBootstrapFile(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return BootstrapFiles.Any(bf =>
            normalized.EndsWith(bf, StringComparison.Ordinal)
            || normalized.Equals(bf, StringComparison.Ordinal));
    }

    private static Result Fail(GuardErrorKind kind, string path, string message)
        => Result.Fail(new GuardError { Kind = kind, Path = path, Message = message });
}
