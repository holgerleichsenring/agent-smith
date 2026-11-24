using System.IO.Compression;
using System.Text;
using AgentSmith.Application.Services.Archives;
using AgentSmith.Application.Services.Pocs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: PoC archives built in memory, and the reader as the composition registers it.</summary>
internal static class PocZips
{
    internal const string Dockerfile = "FROM python:3.12-slim\nWORKDIR /app\nCOPY . .\nEXPOSE 8000\nCMD [\"python\", \"app.py\"]\n";

    internal static PocArchiveReader Reader() =>
        new ServiceCollection().AddArchiveRules().BuildServiceProvider().GetRequiredService<PocArchiveReader>();

    internal static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>An app folder holding a Dockerfile, app.py and <paramref name="extra"/>.</summary>
    internal static byte[] App(params (string Path, byte[] Bytes)[] extra) =>
        Zip([("app/Dockerfile", Text(Dockerfile)), ("app/app.py", Text("print('hi')\n")), .. extra]);

    internal static byte[] Zip(params (string Path, byte[] Bytes)[] files) => Build(files, CompressionLevel.Optimal);

    internal static byte[] Stored(params (string Path, byte[] Bytes)[] files) => Build(files, CompressionLevel.NoCompression);

    /// <summary>A ZIP whose entries the caller builds, e.g. to set their unix mode.</summary>
    internal static byte[] With(Action<ZipArchive> build)
    {
        using var into = new MemoryStream();
        using (var zip = new ZipArchive(into, ZipArchiveMode.Create, leaveOpen: true)) build(zip);
        return into.ToArray();
    }

    internal static void Add(ZipArchive zip, string path, byte[] bytes, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var entry = zip.CreateEntry(path, level).Open();
        entry.Write(bytes);
    }

    /// <summary>The compressed size the central directory records for <paramref name="path"/>.</summary>
    internal static long CompressedSize(byte[] zip, string path)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.GetEntry(path)!.CompressedLength;
    }

    /// <summary><paramref name="zip"/> whose central directory declares <paramref name="size"/> as the unpacked size of
    /// <paramref name="path"/> — what the upload reads without inflating.</summary>
    internal static byte[] Declaring(byte[] zip, string path, uint size) =>
        Patched(zip, path, (bytes, at, central) => { if (central) BitConverter.GetBytes(size).CopyTo(bytes, at + 24); });

    /// <summary><paramref name="zip"/> with general-purpose bit 0 (encrypted) set on <paramref name="path"/>.</summary>
    internal static byte[] Encrypting(byte[] zip, string path) =>
        Patched(zip, path, (bytes, at, central) => bytes[at + (central ? 8 : 6)] |= 1);

    /// <summary><paramref name="zip"/> with every <paramref name="from"/> byte in entry names made <paramref name="to"/>.</summary>
    internal static byte[] Renaming(byte[] zip, byte from, byte to)
    {
        var bytes = (byte[])zip.Clone();
        Each(bytes, (at, central, nameAt, length) =>
        {
            for (var i = nameAt; i < nameAt + length; i++) if (bytes[i] == from) bytes[i] = to;
        });
        return bytes;
    }

    private static byte[] Patched(byte[] zip, string path, Action<byte[], int, bool> patch)
    {
        var bytes = (byte[])zip.Clone();
        Each(bytes, (at, central, nameAt, length) =>
        {
            if (Encoding.UTF8.GetString(bytes, nameAt, length) == path) patch(bytes, at, central);
        });
        return bytes;
    }

    // Every local header and central-directory record: its offset, which it is, and where its name lies.
    private static void Each(byte[] bytes, Action<int, bool, int, int> visit)
    {
        for (var at = 0; at + 46 <= bytes.Length; at++)
        {
            var signature = BitConverter.ToUInt32(bytes, at);
            if (signature == 0x04034b50) visit(at, false, at + 30, BitConverter.ToUInt16(bytes, at + 26));
            if (signature == 0x02014b50) visit(at, true, at + 46, BitConverter.ToUInt16(bytes, at + 28));
        }
    }

    private static byte[] Build((string Path, byte[] Bytes)[] files, CompressionLevel level) =>
        With(zip => { foreach (var (path, bytes) in files) Add(zip, path, bytes, level); });
}
