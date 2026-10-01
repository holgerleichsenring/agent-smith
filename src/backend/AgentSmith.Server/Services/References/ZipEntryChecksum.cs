namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the CRC-32 a ZIP directory records for every entry, recomputed over the bytes
/// actually inflated. The runtime stops inflating at an entry's DECLARED length, so an entry that
/// lies about its size arrives cut short rather than oversized — and only its checksum tells. A
/// set is never stored with a file the archive silently truncated.
/// </summary>
public sealed class ZipEntryChecksum
{
    private const uint Polynomial = 0xEDB88320;
    private static readonly uint[] Table = BuildTable();

    public uint Of(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? Polynomial ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
