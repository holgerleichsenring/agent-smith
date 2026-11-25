using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Sandbox;

public static class SandboxWorkDirectoryExtensions
{
    /// <summary>
    /// 2026-11-25-9992c: the repository root of <paramref name="sandbox"/>: the first root a
    /// sandbox in its wrapper chain declares. Every production sandbox declares one or wraps one
    /// (SandboxRootDeclarationRuleTests), so the workspace root below is what only a test double
    /// that declares nothing is served.
    /// </summary>
    public static string WorkDirectory(this ISandbox sandbox)
    {
        var current = sandbox;
        while (true)
        {
            if (current is ISandboxWorkDirectory rooted) return rooted.WorkDirectory;
            if (current is not ISandboxWrapper wrapper) return Repository.WorkspaceRoot;
            current = wrapper.Inner;
        }
    }

    /// <summary>A path under the sandbox's repository root.</summary>
    public static string PathUnderRoot(this ISandbox sandbox, string relative) =>
        $"{sandbox.WorkDirectory().TrimEnd('/')}/{relative.TrimStart('/')}";
}
