namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933da: the three folders of a PoC target inside its source repo — root, deploy_root and template_path.
/// Each must be a relative path of plain segments, and none may lie inside another, because the steps that write them
/// are scoped to their folder.
/// </summary>
public sealed class PocTargetFolders(PocTargetConfig target)
{
    public IEnumerable<string> Problems()
    {
        var folders = new (string Key, string Path)[]
        {
            ("root", target.Root), ("deploy_root", target.DeployRoot), ("template_path", target.TemplatePath),
        };
        foreach (var (key, path) in folders.Where(f => !IsPlain(f.Path)))
            yield return $"pocs.targets[].{key} of target '{target.Name}' is '{path}'; it must be a relative path without "
                + "'..', a backslash or an empty segment, because every step is scoped to that folder of the source repo";
        foreach (var (inner, outer) in Nested(folders))
            yield return $"pocs.targets[].{inner.Key} of target '{target.Name}' ('{inner.Path}') lies inside "
                + $"{outer.Key} ('{outer.Path}'); keep them apart, because each is written by a different step";
    }

    private static bool IsPlain(string path) =>
        !string.IsNullOrEmpty(path) && !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':')
        && path.Split('/').All(segment => segment.Length > 0 && segment != ".." && segment != ".");

    private static IEnumerable<((string Key, string Path) Inner, (string Key, string Path) Outer)> Nested(
        (string Key, string Path)[] folders) =>
        from inner in folders
        from outer in folders
        where inner.Key != outer.Key && IsPlain(inner.Path) && IsPlain(outer.Path)
              && (inner.Path + "/").StartsWith(outer.Path + "/", StringComparison.Ordinal)
              && (inner.Path != outer.Path || string.CompareOrdinal(inner.Key, outer.Key) > 0)
        select (inner, outer);
}
