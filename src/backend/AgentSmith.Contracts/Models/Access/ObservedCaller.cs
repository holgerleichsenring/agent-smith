namespace AgentSmith.Contracts.Models.Access;

/// <summary>
/// 2026-08-26-7a51: a caller this installation has actually seen, so an administrator
/// PICKS the person they are granting a role to instead of typing an identifier out of a
/// directory console.
/// <para>
/// There is no sign-in to hook: a bearer token is validated on every request and the
/// server holds no session. So this is an observation, not an event — coalesced in memory
/// and written off the request path. It is not configuration: it changes with nobody
/// deciding anything, and it never travels in a config export.
/// </para>
/// <para>
/// Both identifiers are kept. <see cref="Subject"/> is the real <c>sub</c>, which is what
/// an environment admin grant matches; <see cref="NameValue"/> is what the configured name
/// claim carried, which is what a person grant is written against. Storing one and calling
/// it the other is how the two stop being distinguishable.
/// </para>
/// </summary>
public sealed record ObservedCaller(
    string Subject,
    string NameClaim,
    string NameValue,
    IReadOnlyList<string> RoleValues,
    IReadOnlyList<string> GroupValues,
    bool GroupsOmitted,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);
