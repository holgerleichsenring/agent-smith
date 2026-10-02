using System.Buffers.Binary;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: reads an image's pixel size from its header for the four media types the
/// vision path accepts. Null means the bytes are not a readable image of the stated type — a
/// mislabelled or truncated file is refused rather than sent to a provider that would reject it.
/// </summary>
public sealed class ImageDimensionReader
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly JpegDimensionReader _jpeg = new();
    private readonly WebpDimensionReader _webp = new();

    public ImageDimensions? Read(string mediaType, byte[] bytes) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => ReadPng(bytes),
        "image/gif" => ReadGif(bytes),
        "image/jpeg" => _jpeg.Read(bytes),
        "image/webp" => _webp.Read(bytes),
        _ => null,
    };

    private static ImageDimensions? ReadPng(byte[] bytes)
    {
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(PngSignature)) return null;
        return new ImageDimensions(
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)),
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
    }

    private static ImageDimensions? ReadGif(byte[] bytes)
    {
        if (bytes.Length < 10 || bytes[0] != 'G' || bytes[1] != 'I' || bytes[2] != 'F') return null;
        return new ImageDimensions(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8)));
    }
}
