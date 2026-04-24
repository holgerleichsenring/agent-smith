namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Enumerates source files in a repository. Skips binary files, oversized files,
/// gitignored paths (when the scan root is a git repo), and a small safety-net
/// list of directories that are generated output even in non-git scans.
/// </summary>
internal static class SourceFileEnumerator
{
    private const long MaxFileSizeBytes = 1_048_576; // 1 MB

    // Safety net for non-git scans and for things git doesn't track but that should
    // never be scanned (.git/ itself, IDE metadata, common language artifacts).
    // Anything that belongs in a project's .gitignore — site/, dist/, coverage/ — is
    // intentionally not here; gitignore handles those when present.
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "__pycache__", ".vs", ".idea"
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".woff", ".woff2", ".ttf", ".eot",
        ".svg", ".zip", ".tar", ".gz", ".exe", ".dll", ".so", ".dylib",
        ".pdf", ".mp3", ".mp4", ".lock", ".min.js", ".min.css",
        ".md"
    };

    public static IEnumerable<string> EnumerateSourceFiles(string repoPath)
    {
        using var resolver = new GitIgnoreResolver(repoPath);

        var stack = new Stack<string>();
        stack.Push(repoPath);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                if (IsBinaryFile(fileName))
                    continue;

                if (resolver.IsIgnored(file))
                    continue;

                try
                {
                    var info = new FileInfo(file);
                    if (info.Length > MaxFileSizeBytes)
                        continue;
                }
                catch
                {
                    continue;
                }

                yield return file;
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                if (ExcludedDirectories.Contains(dirName))
                    continue;
                if (resolver.IsIgnored(subdir))
                    continue;
                stack.Push(subdir);
            }
        }
    }

    private static bool IsBinaryFile(string fileName)
    {
        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
    }
}
