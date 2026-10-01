namespace AgentSmith.Infrastructure.Models;

/// <summary>2026-10-01-283dd: an image's pixel size, read from its header.</summary>
public sealed record ImageDimensions(int Width, int Height)
{
    public int LongEdge => Math.Max(Width, Height);
}
