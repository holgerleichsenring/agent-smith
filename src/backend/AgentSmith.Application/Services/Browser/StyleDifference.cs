namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: one exact difference between a reference and a candidate — a property of a
/// selector pair the browser computed differently, with both values. A selector that matched
/// nothing on one side is a difference too, under <see cref="MatchProperty"/>.
/// </summary>
public sealed record StyleDifference(
    string ReferenceSelector, string CandidateSelector, string Property, string ReferenceValue, string CandidateValue)
{
    /// <summary>The property a match failure is reported under.</summary>
    public const string MatchProperty = "(element)";

    /// <summary>What a side that matched nothing reads as.</summary>
    public const string NoMatch = "no element matches";
}
