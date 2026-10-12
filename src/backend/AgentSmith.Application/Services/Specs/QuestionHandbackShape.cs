namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0400c: whether a QUESTION hand-back is a question anyone can answer — at least two
/// readings, and a taken index that names one of them. Split from
/// <see cref="SpecDerivationEnvelope"/>, which reads the hand-back and asks this for the verdict.
/// </summary>
internal static class QuestionHandbackShape
{
    private const int MinReadings = 2;

    /// <summary>Why the readings and taken index are refused; null when they form a question.</summary>
    public static string? Rejection(IReadOnlyList<string> readings, int taken)
    {
        if (readings.Count < MinReadings)
            return $"a question hand-back needs at least {MinReadings} readings — "
                + $"{readings.Count} given; if there is only one reading, cut specs under it";
        if (taken < 0 || taken >= readings.Count)
            return $"the taken index {taken} names none of the {readings.Count} readings";
        return null;
    }
}
