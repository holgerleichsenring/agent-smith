using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-26-7a51: everything the access surface renders, in one read. The four panes —
/// people, groups, roles and the claim names — are views of one document plus the callers
/// this installation has observed, so answering them separately would let two of them
/// disagree about the same save.
/// </summary>
/// <param name="Document">
/// The stored mapping exactly as it is, which is what a save sends back. The panes are
/// DERIVED — a custom role's bundle reaches them with the permissions the catalog does not
/// know already dropped — so a surface that rebuilt the document from what it renders
/// would quietly rewrite the very roles this phase promises to round-trip verbatim.
/// </param>
/// <param name="NameClaimIsSelfAsserted">
/// The name claim is not <c>sub</c>. Email and preferred-username are editable by their
/// holder in common directory configurations, so a person who can change their own can
/// claim a grant written for somebody else.
/// </param>
public sealed record AccessView(
    string RoleClaim,
    string GroupClaim,
    string NameClaim,
    RoleMappingConfig Document,
    bool NameClaimIsSelfAsserted,
    int ObservationRetentionDays,
    IReadOnlyList<AccessPersonView> People,
    IReadOnlyList<AccessGroupView> Groups,
    IReadOnlyList<AccessRoleView> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Findings);
