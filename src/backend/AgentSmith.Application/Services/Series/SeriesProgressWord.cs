namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-25-433fa: how far a series is, in one word the series page's status line renders — merged tickets over
/// all of them, the one measure the model holds that never goes back (a green pull request can still turn red, and tickets
/// carry no size). None before the first merge, all once every ticket merged, else the nearest mark.</summary>
public static class SeriesProgressWord
{
    public const string None = "none", All = "all";

    public static string Of(int merged, int total)
    {
        if (total <= 0 || merged <= 0) return None;
        if (merged >= total) return All;
        var share = (double)merged / total;
        (double At, string Word)[] marks =
        [
            (0.1, "begun"), (1 / 4d, "quarter"), (1 / 3d, "third"), (1 / 2d, "half"), (2 / 3d, "twoThirds"),
            (3 / 4d, "threeQuarters"), (0.9, "nearly"),
        ];
        return marks.MinBy(m => Math.Abs(m.At - share)).Word;
    }
}
