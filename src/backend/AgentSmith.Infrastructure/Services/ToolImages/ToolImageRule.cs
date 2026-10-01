using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: the bounds a deposited image must already meet. Encoding and downscaling
/// are the producer's job; this only checks, so an oversized picture is refused by name instead
/// of being sent to a provider that rejects the whole request.
/// </summary>
public sealed class ToolImageRule(ImageDimensionReader dimensions)
{
    public const int MaxLongEdgePixels = 1568;

    /// <summary>Null when the image may be shown; otherwise why it may not.</summary>
    public string? Refusal(ToolImage image)
    {
        if (!TicketImageAttachment.IsSupportedImage(image.MediaType))
            return $"media type '{image.MediaType}' is not one of image/png, image/jpeg, image/gif, image/webp";
        if (image.Bytes.Length == 0)
            return "the image is empty";
        if (image.Bytes.Length > TicketImageAttachment.MaxSizeBytes)
            return $"the image is {image.Bytes.Length} bytes, over the {TicketImageAttachment.MaxSizeBytes}-byte limit";
        var size = dimensions.Read(image.MediaType, image.Bytes);
        if (size is null)
            return $"the bytes are not a readable {image.MediaType} image";
        return size.LongEdge > MaxLongEdgePixels
            ? $"the image is {size.Width}x{size.Height} px; its long edge must be at most {MaxLongEdgePixels} px"
            : null;
    }
}
