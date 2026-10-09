using System.Text;
using System.Text.Unicode;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-09-86e1: how a stored upload file is shown, decided by its CONTENT — so a .py, a
/// Dockerfile or a .env, all stored untyped, read as the text they are. Text is a file whose
/// first 8 KB hold no NUL and decode as UTF-8; an image is a raster type a browser draws as-is;
/// everything else (an SVG included, which can carry script) is a download.
/// </summary>
public sealed class ReferenceFilePreview(ReferenceFileTypes types)
{
    /// <summary>The bytes of text a preview carries at most.</summary>
    public const int MaxTextBytes = 200 * 1024;

    public const string TextKind = "text";
    public const string ImageKind = "image";
    public const string BinaryKind = "binary";

    private const int SniffBytes = 8 * 1024;

    private static readonly HashSet<string> Raster = new(StringComparer.Ordinal)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp",
    };

    public ReferenceFilePreviewView For(string path, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (IsRaster(path)) return new(path, ImageKind, content.LongLength, null, false);
        if (!IsText(content)) return new(path, BinaryKind, content.LongLength, null, false);
        var cut = Boundary(content, Math.Min(content.Length, MaxTextBytes));
        return new(path, TextKind, content.LongLength,
            Encoding.UTF8.GetString(content, 0, cut), cut < content.Length);
    }

    /// <summary>The media type the content route serves the file as: a raster type inline, anything else as bytes.</summary>
    public string ServedAs(string path) => IsRaster(path) ? types.MediaTypeOf(path) : ReferenceFileTypes.Untyped;

    private bool IsRaster(string path) => Raster.Contains(types.MediaTypeOf(path));

    private static bool IsText(byte[] content)
    {
        var sniff = Math.Min(content.Length, SniffBytes);
        return Array.IndexOf(content, (byte)0, 0, sniff) < 0 && Utf8.IsValid(content.AsSpan(0, Boundary(content, sniff)));
    }

    /// <summary>The largest cut at or below <paramref name="at"/> that does not split a UTF-8 character.</summary>
    private static int Boundary(byte[] content, int at)
    {
        if (at >= content.Length) return content.Length;
        var cut = at;
        while (cut > 0 && (content[cut] & 0xC0) == 0x80) cut--;
        return cut;
    }
}
