namespace AgentSmith.Server.Extensions;

/// <summary>2026-10-25-0099b: the median of a set of values — the middle one, or the mean of the middle two; null when empty.</summary>
internal static class MedianExtensions
{
    public static double? MedianOrNull(this IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0) return null;
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
