using System.Text;
using AgentSmith.Server.Services.References;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-09-86e1: a stored upload file is shown by what its bytes are, not by its extension.</summary>
public sealed class ReferenceFilePreviewTests
{
    private readonly ReferenceFilePreview _preview = new(new ReferenceFileTypes());

    [Fact]
    public void For_PythonFile_IsText()
    {
        var shown = _preview.For("app/main.py", Encoding.UTF8.GetBytes("print('grüß')\n"));

        shown.Kind.Should().Be(ReferenceFilePreview.TextKind, "a .py is stored untyped and is still text");
        shown.Text.Should().Be("print('grüß')\n");
        shown.Truncated.Should().BeFalse();
    }

    [Fact]
    public void For_TextOverTheBound_IsCutAtACharacterAndSaysSo()
    {
        var content = Encoding.UTF8.GetBytes(new string('ä', ReferenceFilePreview.MaxTextBytes));

        var shown = _preview.For("big.txt", content);

        shown.Truncated.Should().BeTrue();
        shown.Text!.Length.Should().Be(ReferenceFilePreview.MaxTextBytes / 2, "a two-byte character is never split");
        shown.Bytes.Should().Be(content.LongLength);
    }

    [Fact]
    public void For_BytesWithNul_IsBinary()
    {
        _preview.For("data.txt", [0x41, 0x00, 0x42]).Kind.Should().Be(ReferenceFilePreview.BinaryKind);
    }

    [Fact]
    public void For_PngAndSvg_PngIsAnImageSvgIsNot()
    {
        _preview.For("logo.png", [0x89, 0x50]).Kind.Should().Be(ReferenceFilePreview.ImageKind);
        _preview.For("logo.svg", "<svg/>"u8.ToArray()).Kind.Should().Be(ReferenceFilePreview.TextKind);
        _preview.ServedAs("logo.svg").Should().Be(ReferenceFileTypes.Untyped, "an SVG can carry script");
    }
}
