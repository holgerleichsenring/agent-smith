namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// p0504: one verification command a domain profile brings, in the order the
/// profile lists it. <paramref name="Stage"/> is the human label the verify
/// outcome is reported under; nothing switches on its value.
/// </summary>
public sealed record DomainProfileCommand(string Stage, string Command);
