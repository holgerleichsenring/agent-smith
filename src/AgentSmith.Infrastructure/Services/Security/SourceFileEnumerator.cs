namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Enumerates source files in a repository, excluding binary files and common non-source directories.
/// </summary>
internal static class SourceFileEnumerator
{
    private const long MaxFileSizeBytes = 1_048_576; // 1 MB

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", "dist", "build",
        "vendor", "__pycache__", ".vs", ".idea", "packages"
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
                if (!ExcludedDirectories.Contains(dirName))
                    stack.Push(subdir);
            }
        }
    }

    private static bool IsBinaryFile(string fileName)
    {
        // Check compound extensions like .min.js, .min.css
        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
    }
}
