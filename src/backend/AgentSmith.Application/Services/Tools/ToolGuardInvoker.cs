namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Adapts <see cref="IPathReadGuard"/> / <see cref="IPathWriteGuard"/> for the
/// SandboxToolHost path-aware tools: returns a tool-result string on violation,
/// null on allow, and treats null guards as no-op (legacy compat).
/// </summary>
public sealed class ToolGuardInvoker
{
    private readonly IPathReadGuard? _readGuard;
    private readonly IPathWriteGuard? _writeGuard;

    public ToolGuardInvoker(IPathReadGuard? readGuard, IPathWriteGuard? writeGuard)
    {
        _readGuard = readGuard;
        _writeGuard = writeGuard;
    }

    public string? CheckRead(string path)
        => _readGuard is null ? null : Format(_readGuard.AssertReadable(path));

    public string? CheckWrite(string path)
        => _writeGuard is null ? null : Format(_writeGuard.AssertWritable(path));

    private static string? Format(Result result)
        => result.IsSuccess ? null : $"Error: {result.Error!.Message}";
}
