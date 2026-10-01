using System.Buffers.Binary;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: a JPEG's pixel size from its first start-of-frame segment, walking the
/// segment lengths from the SOI marker.
/// </summary>
public sealed class JpegDimensionReader
{
    public ImageDimensions? Read(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8) return null;
        var position = 2;
        while (position + 9 < bytes.Length)
        {
            if (bytes[position] != 0xFF) return null;
            var marker = bytes[position + 1];
            if (marker == 0xFF) { position++; continue; }
            if (IsStartOfFrame(marker))
                return new ImageDimensions(
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 7)),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 5)));
            position += 2 + BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 2));
        }
        return null;
    }

    // SOF0..SOF15, less DHT (C4), JPG (C8) and DAC (CC), which share the range.
    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
}
