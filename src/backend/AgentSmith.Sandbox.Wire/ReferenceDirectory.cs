namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// 2026-10-01-283df: where a run writes the uploaded website sets its approval cites — inside the
/// carrying repository's working tree, so the master's path tools reach them, and outside every
/// commit and every whole-repository search. One set per directory, named by its id.
/// </summary>
public static class ReferenceDirectory
{
    /// <summary>The directory, relative to the repository root.</summary>
    public const string Path = ".agentsmith/reference";

    /// <summary>The line <c>.git/info/exclude</c> carries — anchored, so a nested project's own
    /// <c>.agentsmith/reference</c> is not excluded with it.</summary>
    public const string ExcludeLine = "/" + Path + "/";

    /// <summary>A pathspec that keeps the directory out of a force-stage, which ignores excludes.</summary>
    public const string ExcludePathspec = ":(exclude)" + Path;

    /// <summary>The directory one set is written to.</summary>
    public static string ForSet(string setId) => $"{Path}/{setId}";
}
