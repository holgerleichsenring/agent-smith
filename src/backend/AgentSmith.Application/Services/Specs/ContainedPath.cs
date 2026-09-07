using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: keeps a model-written path inside the repository. Read-only is not
/// enough: by the time the derivation runs, SetupRegistryAuth has written registry
/// credentials under /root, and a file read or a grep pointed there would read them into
/// the model's context and into the trace. So every path a look names is resolved under
/// /work, and an absolute path or one that climbs out is refused before anything runs.
/// </summary>
internal static class ContainedPath
{
    public const string Refusal =
        "That path is outside the repository. Name a path relative to the repository root.";

    private const string Here = ".";

    /// <summary>The path relative to the repository root, or false when it is absolute,
    /// climbs above the root, or is otherwise not a path into the tree. An absent path is
    /// the root itself.</summary>
    public static bool TryRelative(string? path, out string relative)
    {
        relative = Here;
        if (string.IsNullOrWhiteSpace(path)) return true;
        var trimmed = path.Trim();
        if (trimmed.StartsWith('/') || trimmed.StartsWith('\\') || trimmed.StartsWith('~')
            || trimmed.Contains(':', StringComparison.Ordinal))
            return false;
        var depth = 0;
        foreach (var segment in trimmed.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == Here) continue;
            depth += segment == ".." ? -1 : 1;
            if (depth < 0) return false;
        }
        relative = trimmed.TrimEnd('/');
        if (relative.Length == 0) relative = Here;
        return true;
    }

    /// <summary>The absolute path inside the sandbox, for a reader that takes one.</summary>
    public static string Absolute(string relative) =>
        relative == Here
            ? Repository.SandboxWorkPath
            : $"{Repository.SandboxWorkPath}/{relative}";
}
