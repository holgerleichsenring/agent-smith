using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: the media type an upload's LEADING BYTES say it is.
/// <para>
/// The declared media type is attacker-supplied here, where the ticket path's was
/// tracker-supplied, so the kind is decided from the bytes and the header is never consulted.
/// The set is the ticket path's — <see cref="TicketImageAttachment"/> is where this estate
/// states which image kinds a model is handed — so a kind those bytes do not spell is refused
/// rather than stored as something a model will fail to decode.
/// </para>
/// </summary>
public sealed class ImageKindFromBytes
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();

    /// <summary>The kind, or null when these bytes are not an image this estate supports.</summary>
    public string? Of(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Png)) return "image/png";
        if (bytes.StartsWith(Jpeg)) return "image/jpeg";
        if (bytes.StartsWith(Gif87) || bytes.StartsWith(Gif89)) return "image/gif";
        return IsWebp(bytes) ? "image/webp" : null;
    }

    // RIFF....WEBP — the four-byte length between the two markers is the file's own.
    private static bool IsWebp(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12
        && bytes[..4].SequenceEqual("RIFF"u8)
        && bytes[8..12].SequenceEqual("WEBP"u8);
}
