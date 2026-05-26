namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Validates that a path is writable for the active skill phase. Layered on top of
/// <see cref="IPathReadGuard"/>: writes also require the phase to allow mutation
/// (Implementation/Bootstrap), and Bootstrap further restricts to the bootstrap files.
/// </summary>
public interface IPathWriteGuard
{
    Result AssertWritable(string path);
}
