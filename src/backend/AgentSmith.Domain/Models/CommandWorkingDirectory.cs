namespace AgentSmith.Domain.Models;

/// <summary>
/// p0212: derives the repo-RELATIVE directory in which a context's
/// prerequisites / test command must run. The analyzer already knows where the
/// project lives (ProjectMap.Modules[].Path); the command must run THERE, not
/// at the repo root — e.g. an npm project under `Sample.Client/` fails
/// `npm install` at `/work` (no package.json) but succeeds in its own subtree.
///
/// Rule, in order:
///   1. Operator override: a context.yaml `meta.workdir` other than "." wins.
///   2. Else the longest common DIRECTORY prefix of the module paths — the dir
///      that contains ALL modules. A context = one project subtree by design,
///      so the module paths always share a real root and the common prefix is
///      reliable (not a heuristic for missing data — it is a deterministic
///      assembly of paths the analyzer produced).
///   3. Else (no modules / no shared root) "." — today's behaviour, unchanged.
/// </summary>
public static class CommandWorkingDirectory
{
    /// <summary>
    /// Returns the repo-relative directory ("." for the repo root) to run the
    /// command in, given the analyzer <paramref name="map"/> and the context's
    /// <paramref name="workdir"/> (`meta.workdir`, "." when unset).
    /// </summary>
    public static string Resolve(ProjectMap? map, string? workdir)
    {
        var operatorOverride = Normalize(workdir);
        if (operatorOverride != ".")
            return operatorOverride;

        if (map is null || map.Modules.Count == 0)
            return ".";

        return CommonDirectoryPrefix(map.Modules.Select(m => m.Path));
    }

    private static string CommonDirectoryPrefix(IEnumerable<string> paths)
    {
        var segmentLists = paths
            .Select(Normalize)
            .Select(p => p == "." ? Array.Empty<string>() : p.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        if (segmentLists.Count == 0)
            return ".";

        var prefix = segmentLists[0];
        foreach (var segments in segmentLists.Skip(1))
        {
            var shared = 0;
            while (shared < prefix.Length && shared < segments.Length &&
                   prefix[shared] == segments[shared])
                shared++;
            prefix = prefix[..shared];
            if (prefix.Length == 0)
                return ".";
        }

        return prefix.Length == 0 ? "." : string.Join('/', prefix);
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ".";
        var trimmed = path.Trim().Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? "." : trimmed;
    }
}
