using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-24-f9c5e: what a beat found or did for one repo's final pull request — the series' account to keep, whether it
/// merged, the step's line, and the sentence for the conversation when it is new.</summary>
public sealed record SeriesDeliveryStep(SeriesDelivery? Delivery, bool Merged, string Line, string? Say)
{
    /// <summary>A step that says <paramref name="sentence"/> unless the series said exactly that last time.</summary>
    public static SeriesDeliveryStep Of(SeriesDelivery delivery, string line, string? sentence, bool merged = false) =>
        sentence is null || sentence == delivery.Said
            ? new(delivery, merged, line, null)
            : new(delivery with { Said = sentence }, merged, line, sentence);
}
