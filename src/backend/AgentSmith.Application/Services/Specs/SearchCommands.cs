using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-0eae: the two commands a delivery account may run, and nothing else.
/// <para>
/// Separated from <see cref="BranchSearch"/> because WHAT may be run is a different question
/// from who may run it and how often — and because a ref cannot be read by grep, so reading
/// the base is a second command rather than a flag on the first. Both are fixed argument
/// vectors with no shell: the pattern is one argv element, so nothing a model writes can
/// become a command. Read-only is a property of what CAN be run here, not of what the caller
/// is asked to stick to.
/// </para>
/// </summary>
internal static class SearchCommands
{
    private const int TimeoutSeconds = 90;

    /// <summary>The working tree as the branch carries it now.</summary>
    public static Step OverTree(string pattern, string? path) =>
        Run("grep",
        [
            "-RInE", "--binary-files=without-match",
            "--exclude-dir=bin", "--exclude-dir=obj", "--exclude-dir=.git",
            "--exclude-dir=node_modules", "-e", pattern, "--",
            string.IsNullOrWhiteSpace(path) ? "." : path,
        ]);

    /// <summary>A named ref — the code as it stood before this delivery.</summary>
    public static Step OverRef(string reference, string pattern, string? path) =>
        Run("git",
        [
            "grep", "-InE", "-e", pattern, reference, "--",
            string.IsNullOrWhiteSpace(path) ? "." : path,
            ":(exclude)**/bin/**", ":(exclude)**/obj/**", ":(exclude)**/node_modules/**",
        ]);

    private static Step Run(string command, string[] args) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: command, Args: args, WorkingDirectory: "/work",
            TimeoutSeconds: TimeoutSeconds);
}
