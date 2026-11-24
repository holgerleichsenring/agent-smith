namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81eb: two targets on one source repo must keep their folders apart — a root, deploy_root or
/// template_path of one equal to or inside the other's root or deploy_root would let one target's commit replace or
/// delete the other's files, and a shared template_path would give both one app template the install writes once.
/// </summary>
public sealed class PocSharedRepoFolders(IReadOnlyList<PocTargetConfig> targets)
{
    public IEnumerable<string> Problems()
    {
        var pairs = from a in targets
                    from b in targets
                    where !ReferenceEquals(a, b) && string.Equals(a.SourceRepo, b.SourceRepo, StringComparison.OrdinalIgnoreCase)
                    select (a, b);
        foreach (var (a, b) in pairs)
        {
            foreach (var (key, path) in Folders(a))
            foreach (var (outerKey, outer) in new[] { ("root", b.Root), ("deploy_root", b.DeployRoot) }.Where(o => Inside(path, o.Item2)))
                yield return $"pocs.targets[].{key} of target '{a.Name}' ('{path}') lies in {outerKey} of target '{b.Name}' "
                    + $"('{outer}') on the shared source repo '{a.SourceRepo}'; keep them apart, because one target's commits "
                    + "would replace or delete the other's files";
            if (string.CompareOrdinal(a.Name, b.Name) < 0 && Same(a.TemplatePath, b.TemplatePath))
                yield return $"pocs.targets[].template_path of targets '{a.Name}' and '{b.Name}' is '{a.TemplatePath}' on the "
                    + $"shared source repo '{a.SourceRepo}'; give each its own, because each target's install writes its app template";
        }
    }

    private static (string Key, string Path)[] Folders(PocTargetConfig target) =>
        [("root", target.Root), ("deploy_root", target.DeployRoot), ("template_path", target.TemplatePath)];

    private static bool Inside(string path, string outer) =>
        !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(outer)
        && (path.TrimEnd('/') + "/").StartsWith(outer.TrimEnd('/') + "/", StringComparison.Ordinal);

    private static bool Same(string a, string b) => string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), StringComparison.Ordinal);
}
