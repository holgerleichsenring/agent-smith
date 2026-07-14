namespace AgentSmith.Contracts.Expectations;

/// <summary>
/// p0328: the at-most-one open question an <see cref="ExpectationDraft"/> may
/// carry. Always a concrete A-or-B choice — an open-ended "what do you want?"
/// pushes authoring cost back onto the human, which is exactly the asymmetry
/// the negotiation step exists to exploit.
/// </summary>
public sealed record ExpectationOpenQuestion(
    string Question,
    string OptionA,
    string OptionB);
