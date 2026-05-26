using System.Text;

namespace AgentSmith.Infrastructure.Services.Tools;

/// <summary>
/// Builds minimal tar archives in-memory for Docker cp file transfers.
/// Supports directory entries and regular file entries.
/// </summary>
internal static class TarArchiveBuilder
{
    public static void WriteDirectoryEntry(Stream stream, string dirName)
    {
        var header = new byte[512];
        Encoding.ASCII.GetBytes(dirName).AsSpan().CopyTo(header.AsSpan(0, 100));
        Encoding.ASCII.GetBytes("0000777\0").CopyTo(header, 100);
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 108);
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 116);
        Encoding.ASCII.GetBytes("00000000000\0").CopyTo(header, 124);
        WriteTimestamp(header);
        header[156] = (byte)'5';
        Encoding.ASCII.GetBytes("ustar\0").CopyTo(header, 257);
        Encoding.ASCII.GetBytes("00").CopyTo(header, 263);
        WriteChecksum(header);
        stream.Write(header);
    }

    public static void WriteFileEntry(Stream stream, string fileName, byte[] content)
    {
        var header = new byte[512];
        Encoding.ASCII.GetBytes(fileName).AsSpan().CopyTo(header.AsSpan(0, 100));
        Encoding.ASCII.GetBytes("0000666\0").CopyTo(header, 100);
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 108);
        Encoding.ASCII.GetBytes("0000000\0").CopyTo(header, 116);
        Encoding.ASCII.GetBytes(
            Convert.ToString(content.Length, 8).PadLeft(11, '0') + "\0").CopyTo(header, 124);
        WriteTimestamp(header);
        header[156] = (byte)'0';
        Encoding.ASCII.GetBytes("ustar\0").CopyTo(header, 257);
        Encoding.ASCII.GetBytes("00").CopyTo(header, 263);
        WriteChecksum(header);

        stream.Write(header);
        stream.Write(content);

        var padding = 512 - (content.Length % 512);
        if (padding < 512) stream.Write(new byte[padding]);
    }

    private static void WriteTimestamp(byte[] header)
    {
        Encoding.ASCII.GetBytes(
            Convert.ToString(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 8)
                .PadLeft(11, '0') + "\0").CopyTo(header, 136);
    }

    private static void WriteChecksum(byte[] header)
    {
        Encoding.ASCII.GetBytes("        ").CopyTo(header, 148);
        var checksum = 0;
        for (var i = 0; i < 512; i++) checksum += header[i];
        Encoding.ASCII.GetBytes(
            Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ").CopyTo(header, 148);
    }
}
