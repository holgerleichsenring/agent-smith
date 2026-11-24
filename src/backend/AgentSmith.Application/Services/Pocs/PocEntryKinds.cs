namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: what an entry may be — a regular file or a folder. The unix mode a ZIP records in the high 16 bits
/// of the external attributes names a symlink, a fifo, a device or a socket, none of which a source folder holds and a
/// symlink of which points out of it; an entry without a unix mode (a Windows-made ZIP) is a plain file. An encrypted
/// entry (general-purpose bit 0) cannot be read without its password, so nothing could check it.
/// </summary>
public sealed class PocEntryKinds
{
    private const int FileTypeMask = 0xF000; // not a limit: the S_IFMT bits of a unix mode
    private const int RegularFile = 0x8000; // not a limit: S_IFREG
    private const int Directory = 0x4000; // not a limit: S_IFDIR

    internal PocRuleRefusal? RefusalOf(IReadOnlyList<PocArchiveItem> items)
    {
        foreach (var item in items)
        {
            if (item.Entry.IsEncrypted)
                return Refused($"'{item.Path}' is encrypted; an encrypted entry cannot be read, so nothing could check it.");
            var type = (item.Entry.ExternalAttributes >> 16) & FileTypeMask;
            if (type is not (0 or RegularFile or Directory))
                return Refused($"'{item.Path}' is a {Named(type)}, not a regular file; a PoC's source holds files and "
                    + "folders only, because a link or a special file can reach outside it.");
        }
        return null;
    }

    private static PocRuleRefusal Refused(string reason) => new(PocArchiveChecks.Kinds, reason);

    private static string Named(int type) => type switch
    {
        0xA000 => "symbolic link",
        0x1000 => "named pipe (fifo)",
        0x2000 => "character device",
        0x6000 => "block device",
        0xC000 => "socket",
        _ => "special file",
    };
}
