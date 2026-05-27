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
/// fixtures. Stateless — per-call values come from the caller.
/// </summary>
public sealed class PathWriteGuard(IPathReadGuard readGuard) : IPathWriteGuard
{
    public Result AssertWritable(
        string path, string repoRoot, SkillExecutionPhase phase, string? contextName)
    {
        var readResult = readGuard.AssertReadable(path, repoRoot);
        if (!readResult.IsSuccess)
            return readResult;

        if (phase != SkillExecutionPhase.Implementation && phase != SkillExecutionPhase.Bootstrap)
            return Fail(GuardErrorKind.WriteForbiddenInPhase, path,
                $"writes are forbidden in phase '{phase}'; only Implementation and Bootstrap may write");

        if (phase == SkillExecutionPhase.Bootstrap)
        {
            var bootstrapFiles = BuildBootstrapFileList(contextName);
            if (!IsBootstrapFile(path, bootstrapFiles))
                return Fail(GuardErrorKind.NotInBootstrapFiles, path,
                    $"path '{path}' is not in the bootstrap-allowed file list [{string.Join(", ", bootstrapFiles)}]");
        }

        return Result.Ok();
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

    private static bool IsBootstrapFile(string path, string[] bootstrapFiles)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return bootstrapFiles.Any(bf =>
            normalized.EndsWith(bf, StringComparison.Ordinal)
            || normalized.Equals(bf, StringComparison.Ordinal));
    }

    private static Result Fail(GuardErrorKind kind, string path, string message)
        => Result.Fail(new GuardError { Kind = kind, Path = path, Message = message });
}
