namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the files an operating system leaves in a folder that nobody put there. They
/// are dropped BEFORE the set is checked, because a folder copied off a Mac carries .DS_Store and
/// __MACOSX, and refusing the set for them would refuse every such upload.
/// </summary>
public sealed class ReferenceIgnoreList
{
    private const string MacResourceFolder = "__MACOSX";
    private const string AppleDoublePrefix = "._";
    private static readonly string[] Names = [".DS_Store", "Thumbs.db", "desktop.ini"];

    /// <summary>Whether <paramref name="path"/>, normalised to forward slashes, is such a file.</summary>
    public bool IsIgnored(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/');
        if (segments.Contains(MacResourceFolder, StringComparer.Ordinal)) return true;
        var name = segments[^1];
        return name.StartsWith(AppleDoublePrefix, StringComparison.Ordinal)
            || Names.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}
