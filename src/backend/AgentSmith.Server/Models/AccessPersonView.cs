namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-26-7a51: one person on the access surface — someone this installation has seen,
/// someone an administrator named by hand, or both.
/// <para>
/// Both identifiers are carried and labelled apart. <see cref="Subject"/> is the real
/// <c>sub</c>; <see cref="NameValue"/> is what the configured name claim held, which is
/// what a grant is written against. A surface that showed one and called it the other is
/// how a grant ends up matched against a claim nobody meant.
/// </para>
/// </summary>
/// <param name="Id">What removes this person — the subject when seen, the granted value otherwise.</param>
/// <param name="LastSeen">Absent for somebody added by hand who has not called yet.</param>
public sealed record AccessPersonView(
    string Id,
    string? Subject,
    string NameClaim,
    string NameValue,
    IReadOnlyList<AccessRoleOriginView> DirectoryRoles,
    IReadOnlyList<string> GrantedRoles,
    IReadOnlyList<string> GroupValues,
    bool GroupsOmitted,
    DateTimeOffset? FirstSeen,
    DateTimeOffset? LastSeen);
