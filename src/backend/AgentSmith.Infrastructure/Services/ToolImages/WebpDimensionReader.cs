using System.Buffers.Binary;
using System.Text;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: a WebP's pixel size from the first chunk of its RIFF container — lossy
/// (VP8), lossless (VP8L) or extended (VP8X).
/// </summary>
public sealed class WebpDimensionReader
{
    public ImageDimensions? Read(byte[] bytes)
    {
        if (bytes.Length < 30 || Tag(bytes, 0) != "RIFF" || Tag(bytes, 8) != "WEBP") return null;
        return Tag(bytes, 12) switch
        {
            "VP8 " => new ImageDimensions(
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26)) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28)) & 0x3FFF),
            "VP8L" when bytes[20] == 0x2F => Lossless(bytes),
            "VP8X" => new ImageDimensions(1 + Uint24(bytes, 24), 1 + Uint24(bytes, 27)),
            _ => null,
        };
    }

    private static ImageDimensions Lossless(byte[] bytes)
    {
        var bits = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(21));
        return new ImageDimensions((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
    }

    private static int Uint24(byte[] bytes, int offset) =>
        bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16;

    private static string Tag(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);
}
