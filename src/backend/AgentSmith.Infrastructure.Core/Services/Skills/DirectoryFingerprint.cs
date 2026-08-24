using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0514: a short, stable identity for a directory's file set — every relative
/// path with its length and last-write time, sorted, hashed. Cheap enough to run
/// on every catalog resolve (metadata only, never file contents) and specific
/// enough that an added, removed or edited file changes the answer.
/// </summary>
internal static class DirectoryFingerprint
{
    private const int ShortLength = 12;

    /// <summary>
    /// Fingerprint of <paramref name="directory"/>, or <c>"empty"</c> when the
    /// directory does not exist.
    /// </summary>
    internal static string Of(string directory)
    {
        if (!Directory.Exists(directory)) return "empty";

        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            AppendEntry(builder, directory, file);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..ShortLength];
    }

    private static void AppendEntry(StringBuilder builder, string directory, string file)
    {
        var info = new FileInfo(file);
        builder.Append(Path.GetRelativePath(directory, file).Replace('\\', '/'))
            .Append(' ')
            .Append(info.Length.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }
}
