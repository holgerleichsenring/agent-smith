namespace AgentSmith.Server.Models;

/// <summary>
/// A person ratified version <paramref name="Version"/> of <paramref name="SpecId"/>'s design mock.
/// Kept on the transcript turn until filing stamps it into the spec's <c>ratified_mock</c>.
/// </summary>
public sealed record MockRatificationTurn(string SpecId, int Version, string By);
