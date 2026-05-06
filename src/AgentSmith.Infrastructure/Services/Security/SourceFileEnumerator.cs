using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Enumerates source files in a repository. Two modes:
/// (a) sandbox-routed via ISandboxFileReader (security scan inside /work);
/// (b) host-disk via System.IO (api-scan code-aware analyzers reading the
///     --source-path bind-mount on the host).
/// Both modes skip binary extensions, oversized files, and a small safety-net
/// list of generated/output directories. .gitignore filtering uses the Ignore
/// NuGet package — no LibGit2Sharp dependency.
/// </summary>
internal static class SourceFileEnumerator
{
    private const long MaxFileSizeBytes = 1_048_576;

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

    public static async IAsyncEnumerable<string> EnumerateAsync(
        ISandboxFileReader reader,
        string repoPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var ignore = await GitIgnoreResolver.LoadAsync(reader, repoPath, cancellationToken);
        var entries = await reader.ListAsync(repoPath, maxDepth: 12, cancellationToken);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HasExcludedSegment(entry, repoPath)) continue;
            if (IsBinaryFile(LastSegment(entry))) continue;
            if (ignore.IsIgnored(entry, repoPath)) continue;

            yield return entry;
        }
    }

    public static IEnumerable<string> EnumerateSourceFiles(string repoPath)
    {
        var ignore = HostGitIgnore.Load(repoPath);
        var stack = new Stack<string>();
        stack.Push(repoPath);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var file in files)
            {
                if (IsBinaryFile(Path.GetFileName(file))) continue;
                if (TooLarge(file)) continue;
                if (ignore.IsIgnored(file, repoPath)) continue;
                yield return file;
            }

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var subdir in subdirs)
            {
                if (ExcludedDirectories.Contains(Path.GetFileName(subdir))) continue;
                if (ignore.IsIgnored(subdir, repoPath)) continue;
                stack.Push(subdir);
            }
        }
    }

    private static bool TooLarge(string path)
    {
        try { return new FileInfo(path).Length > MaxFileSizeBytes; }
        catch { return true; }
    }

    private static bool HasExcludedSegment(string fullPath, string repoPath)
    {
        var rel = fullPath.Length > repoPath.Length ? fullPath[repoPath.Length..] : fullPath;
        var segments = rel.TrimStart('/').Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
            if (ExcludedDirectories.Contains(segments[i])) return true;
        return false;
    }

    private static bool IsBinaryFile(string fileName)
    {
        if (fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
    }

    private static string LastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}
