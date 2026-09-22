using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-46ef: what a read-only source scope does with a WRITE, now that the step KIND
/// no longer decides it.
/// <para>
/// A write is refused unless its path lies under one of the prefixes the scope declares, and
/// the declared set is EMPTY by default — so nothing is writable in a source scope until a
/// caller names a prefix. The phase that publishes a specification to a ticket branch is what
/// passes one; until then this policy is a refusal with a reason.
/// </para>
/// <para>
/// The path is canonicalised BEFORE it is matched. The seam that resolves a step's path
/// (<c>FileStepHandler</c>) joins a relative path onto the work root and uses an absolute one
/// verbatim, collapsing nothing — so a prefix match over a raw path would accept
/// <c>specs/../../etc/passwd</c>. Collapsing first also makes the containment leg cheap: a
/// canonical path that is not under the work root is refused whatever the prefix set says.
/// </para>
/// </summary>
public sealed class SourceScopeWritePolicy(IReadOnlyCollection<string> writablePrefixes)
{
    /// <summary>The default: no prefix declared, so every write is refused.</summary>
    public static SourceScopeWritePolicy Nothing => new([]);

    /// <summary>The refusal for a write this scope will not serve; null when it will.</summary>
    public StepResult? Refuse(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var canonical = Canonical(step.Path, step.WorkingDirectory);
        if (!IsUnder(canonical, Repository.SandboxWorkPath))
            return SourceScopeRefusal.Because(step,
                $"'{step.Path}' resolves to '{canonical}', which is outside the checkout at "
                + $"{Repository.SandboxWorkPath}. A read-only source sandbox writes nothing "
                + "outside its own checkout.");

        if (writablePrefixes.Any(prefix => IsUnder(canonical, Rooted(prefix)))) return null;

        return SourceScopeRefusal.Because(step, writablePrefixes.Count == 0
            ? $"'{step.Path}' is not writable: this read-only source sandbox declares no "
              + "writable prefix, so every write is refused."
            : $"'{step.Path}' resolves to '{canonical}', which lies under none of the writable "
              + $"prefixes this source sandbox declares ({string.Join(", ", writablePrefixes)}).");
    }

    /// <summary>A declared prefix as an absolute, collapsed path under the work root.</summary>
    private static string Rooted(string prefix) => Canonical(prefix, Repository.SandboxWorkPath);

    /// <summary>
    /// The path the agent would actually open, with '.' and '..' collapsed. Written out rather
    /// than taken from <c>Path.GetFullPath</c> because the target is a POSIX container path and
    /// must resolve identically on whatever host the server runs on.
    /// </summary>
    internal static string Canonical(string? path, string? workingDirectory)
    {
        var root = string.IsNullOrEmpty(workingDirectory)
            ? Repository.SandboxWorkPath
            : workingDirectory;
        var raw = (path ?? string.Empty).Replace('\\', '/');
        var combined = raw.StartsWith('/') ? raw : $"{root.Replace('\\', '/')}/{raw}";
        var segments = new List<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return "/" + string.Join('/', segments);
    }

    private static bool IsUnder(string canonical, string root) =>
        string.Equals(canonical, root, StringComparison.Ordinal)
        || canonical.StartsWith(root + "/", StringComparison.Ordinal);
}
