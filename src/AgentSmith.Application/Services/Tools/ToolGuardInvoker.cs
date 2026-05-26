using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Adapts <see cref="IPathReadGuard"/> / <see cref="IPathWriteGuard"/> for the
/// SandboxToolHost path-aware tools: carries the per-bootstrap context (repo
/// root, write phase, context name) and forwards it on each guard call.
/// Returns a tool-result string on violation, null on allow, and treats null
/// guards as no-op (legacy compat).
/// </summary>
public sealed class ToolGuardInvoker
{
    private readonly IPathReadGuard? _readGuard;
    private readonly IPathWriteGuard? _writeGuard;
    private readonly string _repoRoot;
    private readonly SkillExecutionPhase _writePhase;
    private readonly string? _contextName;

    public ToolGuardInvoker(
        IPathReadGuard? readGuard,
        IPathWriteGuard? writeGuard,
        string repoRoot,
        SkillExecutionPhase writePhase,
        string? contextName)
    {
        _readGuard = readGuard;
        _writeGuard = writeGuard;
        _repoRoot = repoRoot;
        _writePhase = writePhase;
        _contextName = contextName;
    }

    public string? CheckRead(string path)
        => _readGuard is null ? null : Format(_readGuard.AssertReadable(path, _repoRoot));

    public string? CheckWrite(string path)
        => _writeGuard is null
            ? null
            : Format(_writeGuard.AssertWritable(path, _repoRoot, _writePhase, _contextName));

    private static string? Format(Result result)
        => result.IsSuccess ? null : $"Error: {result.Error!.Message}";
}
