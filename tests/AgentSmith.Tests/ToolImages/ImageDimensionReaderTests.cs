using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.ToolImages;
using FluentAssertions;

namespace AgentSmith.Tests.ToolImages;

/// <summary>2026-10-01-283dd: pixel sizes read from the headers of the four accepted formats.</summary>
public sealed class ImageDimensionReaderTests
{
    private readonly ImageDimensionReader _reader = new();

    [Fact]
    public void Png_ReadsTheIhdrSize() =>
        _reader.Read("image/png", ToolImageLoopFixture.Png(1440, 900)).Should().Be(new ImageDimensions(1440, 900));

    [Fact]
    public void Gif_ReadsTheLogicalScreenSize() =>
        _reader.Read("image/gif", [.. "GIF89a"u8, 0x20, 0x03, 0x58, 0x02]).Should().Be(new ImageDimensions(800, 600));

    [Fact]
    public void Jpeg_SkipsSegmentsToTheStartOfFrame()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x02, 0x58, 0x03, 0x20, 0x03, 0, 0, 0];
        new JpegDimensionReader().Read(jpeg).Should().Be(new ImageDimensions(800, 600));
    }

    [Fact]
    public void Webp_ReadsLossyLosslessAndExtended()
    {
        var reader = new WebpDimensionReader();
        reader.Read(Webp("VP8 ", 26, [0x20, 0x03, 0x58, 0x02])).Should().Be(new ImageDimensions(800, 600));
        var lossless = Webp("VP8L", 20, [0x2F]);
        BitConverter.GetBytes(799u | 599u << 14).CopyTo(lossless, 21);
        reader.Read(lossless).Should().Be(new ImageDimensions(800, 600));
        reader.Read(Webp("VP8X", 24, [0x1F, 0x03, 0x00, 0x57, 0x02, 0x00])).Should().Be(new ImageDimensions(800, 600));
    }

    [Fact]
    public void MislabelledBytes_AreUnreadable()
    {
        _reader.Read("image/jpeg", ToolImageLoopFixture.Png(10, 10)).Should().BeNull();
        _reader.Read("image/webp", ToolImageLoopFixture.Png(10, 10)).Should().BeNull();
        _reader.Read("image/bmp", ToolImageLoopFixture.Png(10, 10)).Should().BeNull();
    }

    private static byte[] Webp(string chunk, int offset, byte[] payload)
    {
        var bytes = new byte[40];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        System.Text.Encoding.ASCII.GetBytes(chunk).CopyTo(bytes, 12);
        payload.CopyTo(bytes, offset);
        return bytes;
    }
}
