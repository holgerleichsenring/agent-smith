namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-08-26-7a51: one row per caller this installation has seen, so an administrator
/// picks a person rather than copying an identifier out of a directory console.
/// <para>
/// Keyed by the real <c>sub</c>, which is the one identifier a directory never reuses.
/// The name claim and its value are stored beside it because a person GRANT is written
/// against a claim, and a row that remembered only one of the two could not say which.
/// </para>
/// <para>
/// The claim values are stored as newline-joined text rather than child rows: nothing
/// queries them, they are shown verbatim, and a child table would put a second write on a
/// path whose whole point is that it costs almost nothing.
/// </para>
/// </summary>
public sealed class ObservedCallerEntity : EntityBase
{
    public string Subject { get; set; } = string.Empty;

    public string NameClaim { get; set; } = string.Empty;

    public string NameValue { get; set; } = string.Empty;

    /// <summary>The role-claim values as they arrived, one per line.</summary>
    public string RoleValues { get; set; } = string.Empty;

    /// <summary>The group-claim values as they arrived, one per line.</summary>
    public string GroupValues { get; set; } = string.Empty;

    /// <summary>
    /// The directory left its group claim out — an overage marker arrived instead. Not
    /// the same state as "carried no groups", and the two have opposite remedies.
    /// </summary>
    public bool GroupsOmitted { get; set; }

    public DateTimeOffset FirstSeen { get; set; }

    public DateTimeOffset LastSeen { get; set; }
}
