namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0514: copies a directory tree over a destination, file by file. A file the
/// source carries replaces its destination counterpart; a file the source does
/// not carry is left alone. That per-file union is what lets a layered catalog
/// keep everything the operator did not write.
/// </summary>
internal static class DirectoryTreeCopier
{
    internal static void CopyOver(string source, string destination)
    {
        if (!Directory.Exists(source)) return;

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
